using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed record class FindNeighborsMessage : MessageContent
{
    [Property(0)]
    public required Address Target { get; init; }

    public override MessageType Type => MessageType.FindNeighbors;
}
