using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

internal interface ISwarmTask
{
    bool IsEnabled { get; }

    Task RunAsync(CancellationToken cancellationToken);
}
