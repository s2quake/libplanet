using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "ChainStatusMessage")]
internal sealed record class ChainStatusMessage : MessageBase
{
    [Property(0)]
    public required int ProtocolVersion { get; init; }

    [Property(1)]
    public required BlockHash GenesisHash { get; init; }

    [Property(2)]
    public required int TipIndex { get; init; }

    public required BlockHash TipHash { get; init; }

    public static implicit operator BlockExcerpt(ChainStatusMessage msg) => new()
    {
        Height = msg.TipIndex,
        ProtocolVersion = msg.ProtocolVersion,
        BlockHash = msg.TipHash,
    };
}
