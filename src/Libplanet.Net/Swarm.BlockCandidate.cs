using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types;

namespace Libplanet.Net;

public partial class Swarm
{
    private readonly ConcurrentDictionary<Peer, int> _processBlockDemandSessions;

    private async Task ConsumeBlockCandidates(
        TimeSpan? checkInterval = null,
        CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (BlockCandidateTable.Count > 0)
            {
                BlockHeader tipHeader = Blockchain.Tip.Header;
                if (BlockCandidateTable.GetCurrentRoundCandidate(Blockchain.Tip) is { } branch)
                {
                    var root = branch.Keys.First();
                    var tip = branch.Keys.Last();
                    _ = BlockCandidateProcess(
                        branch,
                        cancellationToken);
                    BlockAppended.Set();
                }
            }
            else if (checkInterval is { } interval)
            {
                await Task.Delay(interval, cancellationToken);
                continue;
            }
            else
            {
                break;
            }

            BlockCandidateTable.Cleanup(IsBlockNeeded);
        }
    }

    private bool BlockCandidateProcess(
        ImmutableSortedDictionary<Block, BlockCommit> candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            FillBlocksAsyncStarted.Set();
            AppendBranch(
                blockChain: Blockchain,
                candidate: candidate,
                cancellationToken: cancellationToken);
            ProcessFillBlocksFinished.Set();
            return true;
        }
        catch (Exception e)
        {
            FillBlocksAsyncFailed.Set();
            return false;
        }
    }

    private void AppendBranch(
        Blockchain blockChain,
        ImmutableSortedDictionary<Block, BlockCommit> candidate,
        CancellationToken cancellationToken = default)
    {
        Block oldTip = blockChain.Tip;
        Block branchpoint = oldTip;
        List<(Block, BlockCommit)> blocks = ExtractBlocksToAppend(branchpoint, candidate);

        if (!blocks.Any())
        {
        }

        try
        {
            long verifiedBlockCount = 0;

            foreach (var (block, commit) in blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                blockChain.Append(block, commit);

                verifiedBlockCount++;
            }
        }
        catch (Exception e)
        {
            const string dbgMsg = "An exception occurred while appending a block";
            throw;
        }
    }

    private List<(Block, BlockCommit)> ExtractBlocksToAppend(Block branchpoint, ImmutableSortedDictionary<Block, BlockCommit> branch)
    {
        var trimmed = new List<(Block, BlockCommit)>();
        bool matchFound = false;
        foreach (var (key, value) in branch)
        {
            if (matchFound)
            {
                trimmed.Add((key, value));
            }
            else
            {
                matchFound = branchpoint.BlockHash.Equals(key.BlockHash);
            }
        }

        return trimmed;
    }

    private async Task<bool> ProcessBlockDemandAsync(
        BlockDemand demand,
        CancellationToken cancellationToken)
    {
        Peer peer = demand.Peer;

        if (_processBlockDemandSessions.ContainsKey(peer))
        {
            // Another task has spawned for the peer.
            return false;
        }

        var sessionRandom = new Random();

        int sessionId = sessionRandom.Next();

        if (demand.Height <= Blockchain.Tip.Height)
        {
            return false;
        }


        try
        {
            _processBlockDemandSessions.TryAdd(peer, sessionId);
            var result = await BlockCandidateDownload(
                peer: peer,
                blockChain: Blockchain,
                logSessionId: sessionId,
                cancellationToken: cancellationToken);

            BlockReceived.Set();
            return result;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (Exception e)
        {
            const string msg =
                "{SessionId}: Unexpected exception occurred during " +
                nameof(ProcessBlockDemandAsync) + "() from {Peer}";
            return false;
        }
        finally
        {
            // Maybe demand table can be cleaned up here, but it will be eventually
            // cleaned up in FillBlocksAsync()
            _processBlockDemandSessions.TryRemove(peer, out _);
        }
    }

    private async Task<bool> BlockCandidateDownload(
        Peer peer,
        Blockchain blockChain,
        int logSessionId,
        CancellationToken cancellationToken)
    {
        var tipBlockHash = blockChain.Tip.BlockHash;
        Block tip = blockChain.Tip;

        List<BlockHash> hashes = await GetBlockHashes(
            peer: peer,
            blockHash: tipBlockHash,
            cancellationToken: cancellationToken);

        if (!hashes.Any())
        {
            FillBlocksAsyncFailed.Set();
            return false;
        }

        IAsyncEnumerable<(Block, BlockCommit)> blocksAsync = GetBlocksAsync(
            peer,
            hashes,
            cancellationToken);
        try
        {
            var items = await blocksAsync.ToArrayAsync(cancellationToken);
            var branch = items.ToImmutableSortedDictionary(
                item => item.Item1,
                item => item.Item2);
            BlockCandidateTable.Add(tip, branch);
            return true;
        }
        catch (ArgumentException ae)
        {
            return false;
        }
    }
}
