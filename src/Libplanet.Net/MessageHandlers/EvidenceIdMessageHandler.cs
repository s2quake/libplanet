using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class EvidenceIdMessageHandler(EvidenceDemandCollection evidenceDemands)
    : MessageHandlerBase<EvidenceIdMessage>
{
    protected override void OnHandle(EvidenceIdMessage message, MessageEnvelope messageEnvelope)
        => evidenceDemands.AddOrUpdate(new EvidenceDemand(messageEnvelope.Sender, [.. message.Ids]));
}
