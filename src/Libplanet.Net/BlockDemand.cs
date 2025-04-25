using Libplanet.Types.Blocks;

namespace Libplanet.Net;

public readonly struct BlockDemand : IBlockExcerpt
{
    public readonly IBlockExcerpt BlockExcerpt;

    public readonly BoundPeer Peer;

    public readonly DateTimeOffset Timestamp;

    public BlockDemand(IBlockExcerpt blockExcerpt, BoundPeer peer, DateTimeOffset timestamp)
    {
        BlockExcerpt = blockExcerpt;
        Peer = peer;
        Timestamp = timestamp;
    }

    public int ProtocolVersion => BlockExcerpt.ProtocolVersion;

    public long Index => BlockExcerpt.Index;

    public BlockHash Hash => BlockExcerpt.Hash;

    public string ToExcerptString()
    {
        return
            $"{GetType().Name} {{" +
            $" {nameof(ProtocolVersion)} = {ProtocolVersion}," +
            $" {nameof(Index)} = {Index}," +
            $" {nameof(Hash)} = {Hash}," +
            " }";
    }
}
