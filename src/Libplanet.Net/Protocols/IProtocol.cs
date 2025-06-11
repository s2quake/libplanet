using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Protocols;

public interface IProtocol
{
    Task BootstrapAsync(
        IEnumerable<Peer> bootstrapPeers,
        TimeSpan? dialTimeout,
        int depth,
        CancellationToken cancellationToken);

    Task AddPeersAsync(
        IEnumerable<Peer> peers,
        TimeSpan? timeout,
        CancellationToken cancellationToken);

    Task RefreshTableAsync(TimeSpan maxAge, CancellationToken cancellationToken);

    Task RebuildConnectionAsync(int depth, CancellationToken cancellationToken);

    Task CheckReplacementCacheAsync(CancellationToken cancellationToken);
}
