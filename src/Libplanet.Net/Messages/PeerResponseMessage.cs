using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model("PeerResponseMessage", Version = 1)]
public sealed partial record class PeerResponseMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<Peer> Peers { get; init; } = [];
}
