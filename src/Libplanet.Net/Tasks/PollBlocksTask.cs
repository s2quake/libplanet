using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types;

namespace Libplanet.Net.Tasks;

internal sealed class PollBlocksTask(Swarm swarm) : BackgroundServiceBase
{
    private readonly ITransport _transport = swarm.Transport;
    private readonly Blockchain _blockchain = swarm.Blockchain;
    private readonly BlockCandidateTable _blockCandidateTable = swarm.BlockCandidateTable;
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
            await PullBlocksAsync(
                timeout, maximumPollPeers, cancellationToken);
        }
    }

    internal async Task PullBlocksAsync(
        TimeSpan timeout,
        int maximumPollPeers,
        CancellationToken cancellationToken)
    {
        if (maximumPollPeers <= 0)
        {
            return;
        }

        var peersWithBlockExcerpt =
            await swarm.GetPeersWithBlockSummary(
                timeout, maximumPollPeers, cancellationToken);
        await PullBlocksAsync(peersWithBlockExcerpt, cancellationToken);
    }

    private async Task PullBlocksAsync(
        (Peer, BlockSummary)[] peersWithBlockExcerpt,
        CancellationToken cancellationToken)
    {
        if (!peersWithBlockExcerpt.Any())
        {
            return;
        }

        long totalBlocksToDownload = 0L;
        Block tempTip = _blockchain.Tip;
        var blocks = new List<(Block, BlockCommit)>();

        try
        {
            // NOTE: demandBlockHashes is always non-empty.
            (var peer, var demandBlockHashes) = await GetDemandBlockHashes(
                _blockchain,
                peersWithBlockExcerpt,
                cancellationToken);
            totalBlocksToDownload = demandBlockHashes.Length;

            var downloadedBlocks = swarm.Transport.GetBlocksAsync(
                peer,
                demandBlockHashes,
                cancellationToken);

            await foreach (
                (Block block, BlockCommit commit) in
                    downloadedBlocks.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                blocks.Add((block, commit));
            }
        }
        catch (Exception e)
        {
            var msg =
                $"Unexpected exception occurred during {nameof(PullBlocksAsync)}()";
            // _fillBlocksAsyncFailedSubject.OnNext(Unit.Default);
        }
        finally
        {
            if (totalBlocksToDownload > 0)
            {
                try
                {
                    var branch = blocks.ToImmutableSortedDictionary(item => item.Item1, item => item.Item2);
                    swarm.BlockCandidateTable.Add(_blockchain.Tip, branch);
                    // _blockReceivedSubject.OnNext(Unit.Default);
                }
                catch (ArgumentException ae)
                {
                }
            }

            // _processFillBlocksFinishedSubject.OnNext(Unit.Default);
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
