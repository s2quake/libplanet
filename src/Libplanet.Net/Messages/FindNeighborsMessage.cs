using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed partial record class FindNeighborsMessage : MessageContent
{
    [Property(0)]
    public Address Target { get; init; }

    public override MessageType Type => MessageType.FindNeighbors;
}
