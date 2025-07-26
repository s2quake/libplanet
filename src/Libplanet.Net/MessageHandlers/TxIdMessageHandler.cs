using Libplanet.Net.Components;
using Libplanet.Net.Messages;
using Libplanet.Net.Services;

namespace Libplanet.Net.MessageHandlers;

internal sealed class TxIdMessageHandler(TransactionFetcher txFetcher)
    : MessageHandlerBase<TxIdMessage>
{
    protected override void OnHandle(TxIdMessage message, MessageEnvelope messageEnvelope)
    {
        txFetcher.Request(messageEnvelope.Sender, [.. message.Ids]);
        // await replyContext.PongAsync();
    }
}
