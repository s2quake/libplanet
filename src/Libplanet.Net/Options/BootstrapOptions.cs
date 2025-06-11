using Libplanet.Net.Protocols;

namespace Libplanet.Net.Options;

public sealed record class BootstrapOptions
{
    public const int DefaultDialTimeout = 15;

    public TimeSpan DialTimeout { get; init; } = TimeSpan.FromSeconds(DefaultDialTimeout);

    public ImmutableHashSet<Peer> SeedPeers { get; init; } = [];

    public int SearchDepth { get; init; } = Kademlia.MaxDepth;
}
