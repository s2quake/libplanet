using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class TxIdMessageHandler(TxFetcher txFetcher)
    : MessageHandlerBase<TxIdMessage>
{
    protected override void OnHandle(TxIdMessage message, MessageEnvelope messageEnvelope)
    {
        txFetcher.DemandMany(messageEnvelope.Sender, [.. message.Ids]);
        // await replyContext.PongAsync();
    }
}
