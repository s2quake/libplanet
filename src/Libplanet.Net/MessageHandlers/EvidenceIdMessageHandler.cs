using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class EvidenceIdMessageHandler(EvidenceDemandCollection evidenceDemands)
    : MessageHandlerBase<EvidenceIdMessage>
{
    protected override ValueTask OnHandleAsync(
        EvidenceIdMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        evidenceDemands.AddOrUpdate(new EvidenceDemand(messageEnvelope.Sender, [.. message.Ids]));
        return ValueTask.CompletedTask;
    }
}
