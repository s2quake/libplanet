using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class BlockDemand(BlockSummary BlockSummary, Peer Peer, DateTimeOffset Timestamp)
{
    public long Height => BlockSummary.Height;

    public BlockHash BlockHash => BlockSummary.BlockHash;
}
