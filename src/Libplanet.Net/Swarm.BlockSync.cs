using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types;
using Nito.AsyncEx;

namespace Libplanet.Net;

public partial class Swarm
{
    public BlockDemandDictionary BlockDemandTable { get; private set; }

    public BlockCandidateTable BlockCandidateTable { get; private set; }

    internal AsyncAutoResetEvent FillBlocksAsyncStarted { get; } = new AsyncAutoResetEvent();

    internal AsyncAutoResetEvent FillBlocksAsyncFailed { get; } = new AsyncAutoResetEvent();

    internal AsyncAutoResetEvent ProcessFillBlocksFinished { get; } = new AsyncAutoResetEvent();

    internal async Task PullBlocksAsync(
        TimeSpan? timeout,
        int maximumPollPeers,
        CancellationToken cancellationToken)
    {
        if (maximumPollPeers <= 0)
        {
            return;
        }

        List<(Peer, BlockExcerpt)> peersWithBlockExcerpt =
            await GetPeersWithExcerpts(
                timeout, maximumPollPeers, cancellationToken);
        await PullBlocksAsync(peersWithBlockExcerpt, cancellationToken);
    }

    private async Task PullBlocksAsync(
        List<(Peer, BlockExcerpt)> peersWithBlockExcerpt,
        CancellationToken cancellationToken)
    {
        if (!peersWithBlockExcerpt.Any())
        {
            return;
        }

        long totalBlocksToDownload = 0L;
        Block tempTip = Blockchain.Tip;
        var blocks = new List<(Block, BlockCommit)>();

        try
        {
            // NOTE: demandBlockHashes is always non-empty.
            (var peer, var demandBlockHashes) = await GetDemandBlockHashes(
                Blockchain,
                peersWithBlockExcerpt,
                cancellationToken);
            totalBlocksToDownload = demandBlockHashes.Length;

            var downloadedBlocks = GetBlocksAsync(
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
            FillBlocksAsyncFailed.Set();
        }
        finally
        {
            if (totalBlocksToDownload > 0)
            {
                try
                {
                    var branch = blocks.ToImmutableSortedDictionary(item => item.Item1, item => item.Item2);
                    BlockCandidateTable.Add(Blockchain.Tip, branch);
                    BlockReceived.Set();
                }
                catch (ArgumentException ae)
                {
                }
            }

            ProcessFillBlocksFinished.Set();
        }
    }

    private async Task FillBlocksAsync(
        CancellationToken cancellationToken)
    {
        var checkInterval = TimeSpan.FromMilliseconds(100);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (BlockDemandTable.Any())
            {
                foreach (var blockDemand in BlockDemandTable.Values)
                {
                    BlockDemandTable.Remove(blockDemand.Peer);
                    _ = ProcessBlockDemandAsync(
                        blockDemand,
                        cancellationToken);
                }
            }
            else
            {
                await Task.Delay(checkInterval, cancellationToken);
                continue;
            }

            BlockDemandTable.Cleanup(IsBlockNeeded);
        }

    }

    private async Task PollBlocksAsync(
        TimeSpan timeout,
        TimeSpan tipLifespan,
        int maximumPollPeers,
        CancellationToken cancellationToken)
    {
        BlockExcerpt lastTip = Blockchain.Tip;
        DateTimeOffset lastUpdated = DateTimeOffset.UtcNow;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!lastTip.BlockHash.Equals(Blockchain.Tip.BlockHash))
            {
                lastUpdated = DateTimeOffset.UtcNow;
                lastTip = Blockchain.Tip;
            }
            else if (lastUpdated + tipLifespan < DateTimeOffset.UtcNow)
            {
                await PullBlocksAsync(
                    timeout, maximumPollPeers, cancellationToken);
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    private void OnBlockChainTipChanged(TipChangedInfo e)
    {
        if (IsRunning)
        {
            BroadcastBlock(e.Tip);
        }
    }
}
