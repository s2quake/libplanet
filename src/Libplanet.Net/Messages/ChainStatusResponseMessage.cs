using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "ChainStatusResponseMessage")]
internal sealed record class ChainStatusResponseMessage : MessageBase
{
    [Property(0)]
    public required int ProtocolVersion { get; init; }

    [Property(1)]
    public required BlockHash GenesisHash { get; init; }

    [Property(2)]
    public required int TipHeight { get; init; }

    [Property(3)]
    public required BlockHash TipHash { get; init; }

    public static implicit operator BlockSummary(ChainStatusResponseMessage msg) => new()
    {
        Height = msg.TipHeight,
        Version = msg.ProtocolVersion,
        BlockHash = msg.TipHash,
    };
}
