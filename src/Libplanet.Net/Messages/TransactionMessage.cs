using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class TransactionMessage : MessageContent
{
    // public TransactionMessage(byte[] payload)
    // {
    //     Payload = payload;
    // }

    // public TransactionMessage(byte[][] dataFrames)
    // {
    //     Payload = dataFrames[0];
    // }

    [Property(0)]
    public byte[] Payload { get; init; }

    public override MessageType Type => MessageType.Tx;

    // public override IEnumerable<byte[]> DataFrames => new[] { Payload, };
}
