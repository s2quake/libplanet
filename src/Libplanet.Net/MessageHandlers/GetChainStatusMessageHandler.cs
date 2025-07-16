using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class GetChainStatusMessageHandler(Blockchain blockchain)
    : MessageHandlerBase<GetChainStatusMessage>
{
    protected override async ValueTask OnHandleAsync(
        GetChainStatusMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        // This is based on the assumption that genesis block always exists.
        var tip = blockchain.Tip;
        var replyMessage = new ChainStatusMessage
        {
            ProtocolVersion = tip.Version,
            GenesisHash = blockchain.Genesis.BlockHash,
            TipHeight = tip.Height,
            TipHash = tip.BlockHash,
        };

        await replyContext.NextAsync(replyMessage);
    }
}
