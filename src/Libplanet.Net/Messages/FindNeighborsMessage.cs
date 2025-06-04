using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "FindNeighborsMessage")]
public sealed record class FindNeighborsMessage : MessageContent
{
    [Property(0)]
    public Address Target { get; init; }

    public override MessageType Type => MessageType.FindNeighbors;
}
