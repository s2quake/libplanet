using Libplanet.Types.Blocks;

namespace Libplanet.Net;

public readonly record struct BlockDemand(
    BlockExcerpt BlockExcerpt, BoundPeer Peer, DateTimeOffset Timestamp)
{
    public long Index => BlockExcerpt.Index;

    public BlockHash Hash => BlockExcerpt.Hash;

    public static implicit operator BlockExcerpt(BlockDemand blockDemand)
        => blockDemand.BlockExcerpt;

    public string ToExcerptString()
    {
        return
            $"{GetType().Name} {{" +
            $" {nameof(BlockExcerpt.ProtocolVersion)} = {BlockExcerpt.ProtocolVersion}," +
            $" {nameof(BlockExcerpt.Index)} = {BlockExcerpt.Index}," +
            $" {nameof(BlockExcerpt.Hash)} = {BlockExcerpt.Hash}," +
            " }";
    }
}
