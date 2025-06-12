using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "NeighborsMessage")]
public sealed partial record class NeighborsMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<Peer> Found { get; init; } = [];

    public override MessageType Type => MessageType.Neighbors;
}
