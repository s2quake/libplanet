using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class GetBlockHashesMessageHandler(Blockchain blockchain)
    : MessageHandlerBase<GetBlockHashesMessage>
{
    protected override void OnHandle(GetBlockHashesMessage message, MessageEnvelope messageEnvelope)
    {
        var height = blockchain.Blocks[message.BlockHash].Height;
        var hashes = blockchain.Blocks[height..].Select(item => item.BlockHash).ToArray();

        // IReadOnlyList<BlockHash> hashes = BlockChain.FindNextHashes(
        //     getBlockHashes.Locator,
        //     FindNextHashesChunkSize);
        var replyMessage = new BlockHashMessage { BlockHashes = [.. hashes] };

        // await replyContext.NextAsync(replyMessage);
    }
}
