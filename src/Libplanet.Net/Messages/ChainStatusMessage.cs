using Destructurama.Attributed;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class ChainStatusMessage : MessageContent
{
    [Property(0)]
    public required int ProtocolVersion { get; init; }

    [LogAsScalar]
    [Property(1)]
    public required BlockHash GenesisHash { get; init; }

    [Property(2)]
    public required int TipIndex { get; init; }

    [LogAsScalar]
    public required BlockHash TipHash { get; init; }

    public override MessageType Type => MessageType.ChainStatus;

    public static implicit operator BlockExcerpt(ChainStatusMessage msg) => new()
    {
        Height = msg.TipIndex,
        ProtocolVersion = msg.ProtocolVersion,
        BlockHash = msg.TipHash,
    };
}
