using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class GetEvidenceMessageHandler(Blockchain blockchain, AccessLimiter accessLimiter)
    : MessageHandlerBase<GetEvidenceMessage>
{
    protected override async ValueTask OnHandleAsync(
        GetEvidenceMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        using var scope = await accessLimiter.CanAccessAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        var evidenceIds = message.EvidenceIds;
        var evidence = evidenceIds
            .Select(evidenceId => blockchain.PendingEvidences.TryGetValue(evidenceId, out var ev) ? ev : null)
            .OfType<EvidenceBase>()
            .ToArray();

        replyContext.TransferAsync(evidence);
    }
}
