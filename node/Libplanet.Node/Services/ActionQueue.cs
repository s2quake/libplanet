using System.Collections.Concurrent;

namespace Libplanet.Node.Services;

internal sealed class ActionQueue : IAsyncDisposable
{
    private readonly BlockingCollection<System.Action> _actions = [];
    private CancellationTokenSource? _cancellationTokenSource;

    public void Add(System.Action action) => _actions.Add(action);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        try
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                if (_actions.TryTake(out var action))
                {
                    try
                    {
                        action();
                    }
                    catch
                    {
                        // ignored
                    }
                }
                else
                {
                    await Task.Delay(1, _cancellationTokenSource.Token);
                }
            }
        }
        catch
        {
            // ignored
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }
}
