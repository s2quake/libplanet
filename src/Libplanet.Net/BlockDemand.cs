using Libplanet.Types.Blocks;

namespace Libplanet.Net;

public readonly record struct BlockDemand(
    BlockExcerpt BlockExcerpt, BoundPeer Peer, DateTimeOffset Timestamp)
{
    public long Index => BlockExcerpt.Height;

    public BlockHash Hash => BlockExcerpt.BlockHash;

    public static implicit operator BlockExcerpt(BlockDemand blockDemand)
        => blockDemand.BlockExcerpt;

    public string ToExcerptString()
    {
        return
            $"{GetType().Name} {{" +
            $" {nameof(BlockExcerpt.ProtocolVersion)} = {BlockExcerpt.ProtocolVersion}," +
            $" {nameof(BlockExcerpt.Height)} = {BlockExcerpt.Height}," +
            $" {nameof(BlockExcerpt.BlockHash)} = {BlockExcerpt.BlockHash}," +
            " }";
    }
}
