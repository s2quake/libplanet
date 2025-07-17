using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class EvidenceIdMessageHandler(EvidenceFetcher fetcher)
    : MessageHandlerBase<EvidenceIdMessage>
{
    protected override void OnHandle(EvidenceIdMessage message, MessageEnvelope messageEnvelope)
    {
        fetcher.DemandMany(messageEnvelope.Sender, [.. message.Ids]);
    }
}
