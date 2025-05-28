using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed partial record class EvidenceMessage : MessageContent
{
    [Property(0)]
    public ImmutableArray<byte> Payload { get; init; } = [];

    public override MessageType Type => MessageType.Evidence;
}
