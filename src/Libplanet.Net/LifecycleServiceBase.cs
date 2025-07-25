using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

public abstract class LifecycleServiceBase : IAsyncDisposable, ILifecycleService, IRecoverable
{
    private static readonly object _lock = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private LifecycleServiceState _state = LifecycleServiceState.None;
    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationToken _cancellationToken;

    public LifecycleServiceState State => _state;

    public bool IsRunning => _state == LifecycleServiceState.Started;

    public bool IsFaulted => _state == LifecycleServiceState.Faluted;

    public bool IsDisposed => _state == LifecycleServiceState.Disposed;

    protected CancellationToken StoppingToken => _cancellationToken;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SetState([LifecycleServiceState.None], LifecycleServiceState.Transitioning);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var cancellationTokenSource = new CancellationTokenSource();
            using var _ = cancellationToken.Register(cancellationTokenSource.Cancel);
            _cancellationTokenSource = cancellationTokenSource;
            _cancellationToken = _cancellationTokenSource.Token;
            await OnStartAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            SetState(LifecycleServiceState.Started);
        }
        catch
        {
            SetState(LifecycleServiceState.Faluted);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        SetState([LifecycleServiceState.Started], LifecycleServiceState.Transitioning);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            await OnStopAsync(cancellationToken).ConfigureAwait(false);
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            cancellationToken.ThrowIfCancellationRequested();
            SetState(LifecycleServiceState.None);
        }
        catch
        {
            SetState(LifecycleServiceState.Faluted);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RecoverAsync()
    {
        SetState([LifecycleServiceState.Faluted], LifecycleServiceState.Transitioning);

        await _semaphore.WaitAsync();
        try
        {
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            await OnRecoverAsync().ConfigureAwait(false);
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            SetState(LifecycleServiceState.None);
        }
        catch
        {
            SetState(LifecycleServiceState.Faluted);
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
            if (_state != LifecycleServiceState.Disposed)
            {
                if (_cancellationTokenSource is not null)
                {
                    await _cancellationTokenSource.CancelAsync();
                }

                await DisposeAsyncCore().ConfigureAwait(false);
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                SetState(LifecycleServiceState.Disposed);
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
        ObjectDisposedException.ThrowIf(_state == LifecycleServiceState.Disposed, this);
        if (_state != LifecycleServiceState.Started)
        {
            throw new InvalidOperationException($"{this} is not running.");
        }

        return CancellationTokenSource.CreateLinkedTokenSource([_cancellationToken, .. cancellationTokens]);
    }

    private void SetState(IEnumerable<LifecycleServiceState> oldStates, LifecycleServiceState newState)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_state == LifecycleServiceState.Disposed, this);

            if (!oldStates.Contains(_state))
            {
                throw new InvalidOperationException($"Cannot change the state from {_state} to {newState}.");
            }

            _state = newState;
        }
    }

    private void SetState(LifecycleServiceState state)
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
