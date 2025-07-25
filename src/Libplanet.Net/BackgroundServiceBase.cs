using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public abstract class BackgroundServiceBase : ServiceBase
{
    private CancellationTokenSource? _cancellationTokenSource;
    private Task _runTask = Task.CompletedTask;

    protected abstract TimeSpan Interval { get; }

    protected abstract Task ExecuteAsync(CancellationToken cancellationToken);

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _runTask = RunAsync(_cancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        await TaskUtility.TryWait(_runTask);
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _runTask = Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        await base.DisposeAsyncCore();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (await TaskUtility.TryDelay(Interval, cancellationToken))
        {
            try
            {
                await ExecuteAsync(cancellationToken);
            }
            catch
            {
                // do nothing
            }
        }
    }
}
