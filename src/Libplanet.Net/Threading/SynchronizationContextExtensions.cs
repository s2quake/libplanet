using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Threading;

public static class SynchronizationContextExtensions
{
    public static async Task PostAsync(
        this SynchronizationContext @this, Action action, CancellationToken cancellationToken)
    {
        using var resetEvent = new ManualResetEvent(false);
        @this.Post(_ =>
        {
            action();
            resetEvent.Set();
        }, null);

        await Task.Run(resetEvent.WaitOne, cancellationToken);
    }
}
