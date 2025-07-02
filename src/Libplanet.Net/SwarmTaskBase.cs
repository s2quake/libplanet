using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

internal abstract class SwarmTaskBase : ISwarmTask
{
    protected abstract TimeSpan Interval { get; }

    protected abstract Task ExecuteAsync(CancellationToken cancellationToken);

    async Task ISwarmTask.RunAsync(CancellationToken cancellationToken)
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
