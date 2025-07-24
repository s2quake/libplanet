using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class EvidenceRequestMessageHandler(Blockchain blockchain, AccessLimiter accessLimiter)
    : MessageHandlerBase<EvidenceRequestMessage>
{
    protected override void OnHandle(EvidenceRequestMessage message, MessageEnvelope messageEnvelope)
    {
        // using var scope = await accessLimiter.CanAccessAsync(cancellationToken);
        // if (scope is null)
        // {
        //     return;
        // }

        // var evidenceIds = message.EvidenceIds;
        // var evidence = evidenceIds
        //     .Select(evidenceId => blockchain.PendingEvidences.TryGetValue(evidenceId, out var ev) ? ev : null)
        //     .OfType<EvidenceBase>()
        //     .ToArray();

        // replyContext.TransferAsync(evidence);
    }
}
