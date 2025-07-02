using Libplanet.Net.Protocols;

namespace Libplanet.Net.Options;

public sealed record class BootstrapOptions
{
    public bool Enabled { get; init; } = true;

    public TimeSpan DialTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public ImmutableHashSet<Peer> SeedPeers { get; init; } = [];

    public int SearchDepth { get; init; } = Kademlia.MaxDepth;
}
