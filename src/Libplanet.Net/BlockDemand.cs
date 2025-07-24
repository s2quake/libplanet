using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class BlockDemand(Peer Peer, BlockSummary BlockSummary, DateTimeOffset Timestamp)
{
    public long Height => BlockSummary.Height;

    public BlockHash BlockHash => BlockSummary.BlockHash;

    public bool IsStale(TimeSpan staleThreshold) => Timestamp + staleThreshold < DateTimeOffset.UtcNow;
}
