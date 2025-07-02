using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class BlockDemand(BlockSummary BlockExcerpt, Peer Peer, DateTimeOffset Timestamp)
{
    public long Height => BlockExcerpt.Height;

    public BlockHash BlockHash => BlockExcerpt.BlockHash;
}
