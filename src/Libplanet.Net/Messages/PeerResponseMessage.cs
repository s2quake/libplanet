using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "PeerResponseMessage")]
public sealed partial record class PeerResponseMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<Peer> Peers { get; init; } = [];
}
