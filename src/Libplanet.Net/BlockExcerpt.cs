using Libplanet.Types.Blocks;

namespace Libplanet.Net;

public sealed record class BlockExcerpt
{
    public int ProtocolVersion { get; init; }

    public long Index { get; init; }

    public BlockHash Hash { get; init; }

    public static implicit operator BlockExcerpt(BlockHeader blockHeader)
        => new()
        {
            ProtocolVersion = blockHeader.ProtocolVersion,
            Index = blockHeader.Height,
            Hash = blockHeader.BlockHash,
        };
}
