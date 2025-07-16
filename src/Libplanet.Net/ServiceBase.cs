using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

public abstract class ServiceBase : IAsyncDisposable, IService, IRecoverable
{
    private static readonly object _lock = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private ServiceState _state = ServiceState.None;
    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationToken _cancellationToken;

    public ServiceState State => _state;

    public bool IsRunning => _state == ServiceState.Started;

    public bool IsFaulted => _state == ServiceState.Faluted;

    public bool IsDisposed => _state == ServiceState.Disposed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SetState([ServiceState.None], ServiceState.Transitioning);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var cancellationTokenSource = new CancellationTokenSource();
            using var _ = cancellationToken.Register(cancellationTokenSource.Cancel);
            _cancellationTokenSource = cancellationTokenSource;
            _cancellationToken = _cancellationTokenSource.Token;
            await OnStartAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            SetState(ServiceState.Started);
        }
        catch
        {
            SetState(ServiceState.Faluted);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        SetState([ServiceState.Started], ServiceState.Transitioning);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await OnStopAsync(cancellationToken).ConfigureAwait(false);
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
                _cancellationToken = default;
            }

            cancellationToken.ThrowIfCancellationRequested();
            SetState(ServiceState.None);
        }
        catch
        {
            SetState(ServiceState.Faluted);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RecoverAsync()
    {
        SetState([ServiceState.Faluted], ServiceState.Transitioning);

        await _semaphore.WaitAsync();
        try
        {
            await OnRecoverAsync().ConfigureAwait(false);
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
                _cancellationToken = default;
            }

            SetState(ServiceState.None);
        }
        catch
        {
            SetState(ServiceState.Faluted);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_state != ServiceState.Disposed)
            {
                await DisposeAsyncCore().ConfigureAwait(false);
                if (_cancellationTokenSource is not null)
                {
                    await _cancellationTokenSource.CancelAsync();
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                    _cancellationToken = default;
                }

                SetState(ServiceState.Disposed);
                GC.SuppressFinalize(this);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    protected abstract Task OnStartAsync(CancellationToken cancellationToken);

    protected abstract Task OnStopAsync(CancellationToken cancellationToken);

    protected virtual async Task OnRecoverAsync()
    {
        await Task.CompletedTask;
        throw new NotSupportedException(
            "RecoverAsync is not supported by default. Override this method to implement recovery logic.");
    }

    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

    protected CancellationTokenSource CreateCancellationTokenSource(params CancellationToken[] cancellationTokens)
    {
        ObjectDisposedException.ThrowIf(_state == ServiceState.Disposed, this);

        if (_state != ServiceState.Started)
        {
            throw new InvalidOperationException($"Cannot create a cancellation token source in the state of {_state}.");
        }

        return CancellationTokenSource.CreateLinkedTokenSource([_cancellationToken, .. cancellationTokens]);
    }

    private void SetState(IEnumerable<ServiceState> oldStates, ServiceState newState)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_state == ServiceState.Disposed, this);

            if (!oldStates.Contains(_state))
            {
                throw new InvalidOperationException($"Cannot change the state from {_state} to {newState}.");
            }

            _state = newState;
        }
    }

    private void SetState(ServiceState state)
    {
        lock (_lock)
        {
            if (_state == state)
            {
                throw new UnreachableException("Cannot change the state to the same value.");
            }

            _state = state;
        }
    }
}
