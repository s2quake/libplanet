using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Tasks;

internal sealed class FillBlocksTask(Swarm swarm) : BackgroundServiceBase
{
    private readonly ITransport _transport = swarm.Transport;
    private readonly Blockchain _blockchain = swarm.Blockchain;
    private readonly BlockBranchCollection _blockCandidateTable = swarm.BlockBranches;
    private readonly ConcurrentDictionary<Peer, int> _processBlockDemandSessions = new();

    protected override TimeSpan Interval => TimeSpan.FromMilliseconds(100);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var blockDemans = swarm.BlockDemandDictionary;
        if (blockDemans.Count > 0)
        {
            foreach (var blockDemand in blockDemans.Values)
            {
                blockDemans.Remove(blockDemand.Peer);
                _ = ProcessBlockDemandAsync(blockDemand, cancellationToken);
            }

            blockDemans.Cleanup(IsBlockNeeded);
        }

    }

    private bool IsBlockNeeded(BlockSummary target) => target.Height > _blockchain.Tip.Height;

    private async Task<bool> ProcessBlockDemandAsync(BlockDemand demand, CancellationToken cancellationToken)
    {
        Peer peer = demand.Peer;

        if (_processBlockDemandSessions.ContainsKey(peer))
        {
            // Another task has spawned for the peer.
            return false;
        }

        var sessionRandom = new Random();

        int sessionId = sessionRandom.Next();

        if (demand.Height <= _blockchain.Tip.Height)
        {
            return false;
        }


        try
        {
            _processBlockDemandSessions.TryAdd(peer, sessionId);
            var result = await BlockCandidateDownload(
                peer: peer,
                blockChain: _blockchain,
                logSessionId: sessionId,
                cancellationToken: cancellationToken);

            // _blockReceivedSubject.OnNext(Unit.Default);
            return result;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (Exception)
        {
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

        var hashes = await GetBlockHashes(
            peer: peer,
            blockHash: tipBlockHash,
            cancellationToken: cancellationToken);

        if (hashes.Length == 0)
        {
            // _fillBlocksAsyncFailedSubject.OnNext(Unit.Default);
            return false;
        }

        var query = GetBlocksAsync(peer, hashes, cancellationToken);
        try
        {
            var blockPairs = await query.ToArrayAsync(cancellationToken);
            var blockBranch = new BlockBranch
            {
                Blocks = [.. blockPairs.Select(item => item.Item1)],
                BlockCommits = [.. blockPairs.Select(item => item.Item2)],
            };
            _blockCandidateTable.Add(tip.BlockHash, blockBranch);
            return true;
        }
        catch (ArgumentException ae)
        {
            return false;
        }
    }

    // FIXME: This would be better if it's merged with GetDemandBlockHashes
    internal async Task<BlockHash[]> GetBlockHashes(
        Peer peer,
        BlockHash blockHash,
        CancellationToken cancellationToken = default)
    {
        return await _transport.GetBlockHashesAsync(peer, blockHash, cancellationToken);
        // var request = new GetBlockHashesMessage { BlockHash = blockHash };
        // MessageEnvelope parsedMessage;
        // try
        // {
        //     parsedMessage = await _transport.SendAsync(
        //         peer,
        //         request,
        //         cancellationToken: cancellationToken).ConfigureAwait(false);
        // }
        // catch (CommunicationException)
        // {
        //     return [];
        // }

        // if (parsedMessage.Message is BlockHashesMessage blockHashes)
        // {
        //     if (blockHashes.Hashes.Any() && blockHash.Equals(blockHashes.Hashes.First()))
        //     {
        //         return [.. blockHashes.Hashes];
        //     }
        // }

        // return [];
    }

    internal async IAsyncEnumerable<(Block, BlockCommit)> GetBlocksAsync(
        Peer peer,
        BlockHash[] blockHashes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new BlockRequestMessage { BlockHashes = [.. blockHashes] };
        int hashCount = blockHashes.Length;

        if (hashCount < 1)
        {
            yield break;
        }

        // TimeSpan blockRecvTimeout = Options.TimeoutOptions.GetBlocksBaseTimeout
        //     + Options.TimeoutOptions.GetBlocksPerBlockHashTimeout.Multiply(hashCount);
        // if (blockRecvTimeout > Options.TimeoutOptions.MaxTimeout)
        // {
        //     blockRecvTimeout = Options.TimeoutOptions.MaxTimeout;
        // }

        // var messageEnvelope = await _transport.SendMessageAsync(peer, request, cancellationToken);
        // var aggregateMessage = (AggregateMessage)messageEnvelope.Message;

        // int count = 0;

        // foreach (var message in aggregateMessage.Messages)
        // {
        //     cancellationToken.ThrowIfCancellationRequested();

        //     if (message is BlocksMessage blockMessage)
        //     {
        //         var payloads = blockMessage.Payloads;
        //         for (int i = 0; i < payloads.Length; i += 2)
        //         {
        //             cancellationToken.ThrowIfCancellationRequested();
        //             byte[] blockPayload = payloads[i];
        //             byte[] commitPayload = payloads[i + 1];
        //             Block block = ModelSerializer.DeserializeFromBytes<Block>(blockPayload);
        //             BlockCommit commit = commitPayload.Length == 0
        //                 ? default
        //                 : ModelSerializer.DeserializeFromBytes<BlockCommit>(commitPayload);

        //             if (count < blockHashes.Length)
        //             {
        //                 if (blockHashes[count].Equals(block.BlockHash))
        //                 {
        //                     yield return (block, commit);
        //                     count++;
        //                 }
        //                 else
        //                 {
        //                     yield break;
        //                 }
        //             }
        //             else
        //             {
        //                 yield break;
        //             }
        //         }
        //     }
        //     else
        //     {
        //         string errorMessage =
        //             $"Expected a {nameof(BlocksMessage)} message as a response of " +
        //             $"the {nameof(GetBlocksMessage)} message, but got a {message.GetType().Name} " +
        //             $"message instead: {message}";
        //         throw new InvalidMessageContractException(errorMessage);
        //     }
        // }
    }
}
