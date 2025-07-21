using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class ChainStatusRequestMessageHandler(Blockchain blockchain)
    : MessageHandlerBase<ChainStatusRequestMessage>
{
    protected override void OnHandle(ChainStatusRequestMessage message, MessageEnvelope messageEnvelope)
    {
        // This is based on the assumption that genesis block always exists.
        var tip = blockchain.Tip;
        var replyMessage = new ChainStatusResponseMessage
        {
            ProtocolVersion = tip.Version,
            GenesisHash = blockchain.Genesis.BlockHash,
            TipHeight = tip.Height,
            TipHash = tip.BlockHash,
        };

        // await replyContext.NextAsync(replyMessage);
    }
}
