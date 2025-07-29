using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class TxIdMessageHandler(TransactionDemandCollection transactionDemands)
    : MessageHandlerBase<TxIdMessage>
{
    protected override void OnHandle(TxIdMessage message, MessageEnvelope messageEnvelope)
    {
        var demand = new TransactionDemand(messageEnvelope.Sender, [.. message.Ids]);
        transactionDemands.AddOrUpdate(demand);
    }
}
