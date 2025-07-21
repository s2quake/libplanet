using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class GetBlockMessageHandler(Blockchain blockchain, AccessLimiter accessLimiter)
    : MessageHandlerBase<BlockRequestMessage>
{
    protected override void OnHandle(
        BlockRequestMessage message, MessageEnvelope messageEnvelope)
    {
        // using var scope = await accessLimiter.CanAccessAsync(cancellationToken);
        // if (scope is null)
        // {
        //     return;
        // }

        // var blockHashes = message.BlockHashes;
        // var blockList = new List<Block>();
        // var blockCommitList = new List<BlockCommit>();
        // foreach (var blockHash in blockHashes)
        // {
        //     if (blockchain.Blocks.TryGetValue(blockHash, out var block)
        //         && blockchain.BlockCommits.TryGetValue(block.BlockHash, out var blockCommit))
        //     {
        //         blockList.Add(block);
        //         blockCommitList.Add(blockCommit);
        //     }

        //     if (blockList.Count == message.ChunkSize)
        //     {
        //         await replyContext.TransferAsync([.. blockList], [.. blockCommitList], hasNext: true);
        //         blockList.Clear();
        //         blockCommitList.Clear();
        //     }
        // }

        // await replyContext.TransferAsync([.. blockList], [.. blockCommitList]);
    }
}
