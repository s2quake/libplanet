using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class TxIdMessageHandler(TransactionDemandCollection transactionDemands)
    : MessageHandlerBase<TxIdMessage>
{
    protected override ValueTask OnHandleAsync(
        TxIdMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        transactionDemands.AddOrUpdate(new TransactionDemand(messageEnvelope.Sender, [.. message.Ids]));
        return ValueTask.CompletedTask;
    }
}
