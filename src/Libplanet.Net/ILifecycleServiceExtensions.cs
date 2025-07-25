using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

public static class ILifecycleServiceExtensions
{
    public static async Task RestartAsync(this ILifecycleService @this, CancellationToken cancellationToken)
    {
        await @this.StopAsync(cancellationToken);
        await @this.StartAsync(cancellationToken);
    }
}
