using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Libplanet.Net;

public abstract partial class ServiceBase(ILogger logger) : IAsyncDisposable, IService, IRecoverable
{
    private static readonly object _lock = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private ServiceState _state = ServiceState.None;
    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationToken _cancellationToken;
    private string _name = string.Empty;

    protected ServiceBase()
        : this(NullLogger<ServiceBase>.Instance)
    {
    }

    public ServiceState State => _state;

    public string Name
    {
        get => _name == string.Empty ? DefaultName : _name;
        init => _name = value;
    }

    public bool IsRunning => _state == ServiceState.Started;

    public bool IsFaulted => _state == ServiceState.Faulted;

    public bool IsDisposed => _state == ServiceState.Disposed;

    protected CancellationToken StoppingToken => _cancellationToken;

    protected virtual string DefaultName => $"{GetType().Name} {GetHashCode()}";

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
            LogStarted(logger, Name);
        }
        catch (Exception e)
        {
            SetState(ServiceState.Faulted);
            LogStartFailed(logger, Name, e);
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
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            await OnStopAsync(cancellationToken).ConfigureAwait(false);
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            cancellationToken.ThrowIfCancellationRequested();
            SetState(ServiceState.None);
            LogStopped(logger, Name);
        }
        catch (Exception e)
        {
            SetState(ServiceState.Faulted);
            LogStopFailed(logger, Name, e);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RecoverAsync()
    {
        SetState([ServiceState.Faulted], ServiceState.Transitioning);

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
            SetState(ServiceState.None);
            LogRecovered(logger, Name);
        }
        catch (Exception e)
        {
            SetState(ServiceState.Faulted);
            LogRecoverFailed(logger, Name, e);
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
                SetState(ServiceState.Transitioning);
                if (_cancellationTokenSource is not null)
                {
                    await _cancellationTokenSource.CancelAsync();
                }

                await DisposeAsyncCore().ConfigureAwait(false);
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                SetState(ServiceState.Disposed);
                LogDisposed(logger, Name);
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
            throw new InvalidOperationException($"{this} is not running.");
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
