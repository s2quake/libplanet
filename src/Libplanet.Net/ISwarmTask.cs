using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

internal interface ISwarmTask
{
    Task RunAsync(CancellationToken cancellationToken);
}
