using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class TransactionMessage : MessageContent
{
    [Property(0)]
    public byte[] Payload { get; init; } = [];

    public override MessageType Type => MessageType.Tx;
}
