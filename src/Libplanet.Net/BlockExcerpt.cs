using Libplanet.Types.Blocks;

namespace Libplanet.Net;

public sealed record class BlockExcerpt
{
    public int ProtocolVersion { get; init; }

    public int Height { get; init; }

    public BlockHash BlockHash { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public static implicit operator BlockExcerpt(Block block)
        => new()
        {
            ProtocolVersion = block.Header.ProtocolVersion,
            Height = block.Header.Height,
            BlockHash = block.BlockHash,
            Timestamp = block.Header.Timestamp,
        };
}
