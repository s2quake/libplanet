using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

public abstract class ServiceBase : IAsyncDisposable
{
    private static readonly object _lock = new();
    private ServiceState _state = ServiceState.None;
    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationToken _cancellationToken;

    public ServiceState State => _state;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SetState([ServiceState.None], ServiceState.Transitioning);

        try
        {
            var cancellationTokenSource = new CancellationTokenSource();
            using var _ = cancellationToken.Register(cancellationTokenSource.Cancel);
            _cancellationTokenSource = cancellationTokenSource;
            _cancellationToken = _cancellationTokenSource.Token;
            await OnStartAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SetState(ServiceState.Started);
        }
        catch
        {
            SetState(ServiceState.Faluted);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (SetState(ServiceState.Transitioning) is { } prevState && prevState != ServiceState.Started)
        {
            throw new InvalidOperationException($"Cannot stop service in the state of {prevState}.");
        }

        try
        {
            await OnStopAsync(cancellationToken);
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
    }

    public async Task RecoverAsync()
    {
        if (SetState(ServiceState.Transitioning) is { } prevState && prevState != ServiceState.Faluted)
        {
            throw new InvalidOperationException($"Cannot recover service in the state of {prevState}.");
        }

        await OnRecoverAsync();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _cancellationToken = default;
        SetState(ServiceState.None);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        SetState(ServiceState.Disposed);
        GC.SuppressFinalize(this);
    }

    protected abstract Task OnStartAsync(CancellationToken cancellationToken);

    protected abstract Task OnStopAsync(CancellationToken cancellationToken);

    protected abstract Task OnRecoverAsync();

    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

    protected CancellationTokenSource CreateCancellationTokenSource()
    {
        ObjectDisposedException.ThrowIf(_state == ServiceState.Disposed, this);

        if (_state != ServiceState.Started)
        {
            throw new InvalidOperationException($"Cannot create a cancellation token source in the state of {_state}.");
        }

        return CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
    }

    private void SetState(IEnumerable<ServiceState> oldStates, ServiceState newState)
    {
        lock (_lock)
        {
            if (!oldStates.Contains(_state))
            {
                throw new InvalidOperationException($"Cannot change the state from {_state} to {newState}.");
            }

            _state = newState;
        }
    }
}
