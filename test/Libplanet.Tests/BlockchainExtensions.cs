using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Tests;

public static class BlockchainExtensions
{
    public static async Task WaitUntilHeightAsync(this Libplanet.Blockchain @this, int height, CancellationToken cancellationToken)
    {
        using var resetEvent = new ManualResetEvent(false);
        using var _ = @this.TipChanged.Subscribe(e =>
        {
            if (e.Tip.Height == height)
            {
                resetEvent.Set();
            }
        });

        while (@this.Tip.Height < height && !resetEvent.WaitOne(0))
        {
            await Task.Delay(100, cancellationToken);
        }
    }
}
