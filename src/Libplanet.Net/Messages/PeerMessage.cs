using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "PeerMessage")]
public sealed partial record class PeerMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<Peer> Peers { get; init; } = [];
}
