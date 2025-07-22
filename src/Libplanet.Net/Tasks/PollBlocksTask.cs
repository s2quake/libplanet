using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types;

namespace Libplanet.Net.Tasks;

internal sealed class PollBlocksTask(Swarm swarm) : BackgroundServiceBase
{
    private readonly ITransport _transport = swarm.Transport;
    private readonly Blockchain _blockchain = swarm.Blockchain;
    private readonly BlockBranchCollection _blockCandidateTable = swarm.BlockBranches;
    private readonly ConcurrentDictionary<Peer, int> _processBlockDemandSessions = new();

    private Block _lastTip = swarm.Blockchain.Tip;
    private DateTimeOffset _lastUpdated = DateTimeOffset.UtcNow;


    protected override TimeSpan Interval => TimeSpan.FromMilliseconds(100);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // BlockExcerpt lastTip = _blockchain.Tip;
        // DateTimeOffset lastUpdated = DateTimeOffset.UtcNow;
        var timeout = swarm.Options.TimeoutOptions.DialTimeout;
        var tipLifespan = swarm.Options.TipLifespan;
        var maximumPollPeers = swarm.Options.MaximumPollPeers;
        if (!_lastTip.BlockHash.Equals(_blockchain.Tip.BlockHash))
        {
            _lastUpdated = DateTimeOffset.UtcNow;
            _lastTip = _blockchain.Tip;
        }
        else if (_lastUpdated + tipLifespan < DateTimeOffset.UtcNow)
        {
            await swarm.PullBlocksAsync(
                timeout, maximumPollPeers, cancellationToken);
        }
    }

    internal bool IsBlockNeeded(BlockSummary target) => target.Height > _blockchain.Tip.Height;

    internal async Task<(Peer, BlockHash[])> GetDemandBlockHashes(
        Blockchain blockchain,
        IList<(Peer, BlockSummary)> peersWithExcerpts,
        CancellationToken cancellationToken = default)
    {
        var exceptions = new List<Exception>();
        foreach ((Peer peer, BlockSummary excerpt) in peersWithExcerpts)
        {
            if (!IsBlockNeeded(excerpt))
            {
                continue;
            }

            try
            {
                var downloadedHashes = await GetDemandBlockHashesFromPeer(
                    blockchain,
                    peer,
                    excerpt,
                    cancellationToken);
                if (downloadedHashes.Length != 0)
                {
                    return (peer, downloadedHashes);
                }
                else
                {
                    continue;
                }
            }
            catch (Exception e)
            {
                exceptions.Add(e);
                continue;
            }
        }

        Peer[] peers = peersWithExcerpts.Select(p => p.Item1).ToArray();
        throw new AggregateException(
            "Failed to fetch demand block hashes from peers: " +
            string.Join(", ", peers.Select(p => p.ToString())),
            exceptions);
    }

    internal async Task<BlockHash[]> GetDemandBlockHashesFromPeer(
        Blockchain blockchain,
        Peer peer,
        BlockSummary excerpt,
        CancellationToken cancellationToken = default)
    {
        var blockHashList = new List<BlockHash>();
        var blockHashes = await swarm.Transport.GetBlockHashesAsync(
            peer: peer,
            blockHash: blockchain.Tip.BlockHash,
            cancellationToken: cancellationToken);

        foreach (var blockHash in blockHashes)
        {
            blockHashList.Add(blockHash);
        }

        return [.. blockHashList];
    }
}
