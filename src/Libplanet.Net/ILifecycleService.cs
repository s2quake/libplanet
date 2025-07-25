using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

public interface ILifecycleService
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
