using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Tests;

public static class SwarmExtensions
{
    public static Task AddPeersAsync(this Swarm @this, ImmutableArray<Peer> peers, CancellationToken cancellationToken)
        => @this.PeerService.AddOrUpdateManyAsync(peers, cancellationToken);
}
