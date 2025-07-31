using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class EvidenceRequestMessageHandler(Blockchain blockchain, ITransport transport, int maxAccessCount)
    : MessageHandlerBase<EvidenceRequestMessage>, IDisposable
{
    private readonly AccessLimiter _accessLimiter = new(maxAccessCount);

    public void Dispose() => _accessLimiter.Dispose();

    protected override void OnHandle(EvidenceRequestMessage message, MessageEnvelope messageEnvelope)
    {
        _ = OnHandleAsync(message, messageEnvelope, default).AsTask();
    }

    private async ValueTask OnHandleAsync(
        EvidenceRequestMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        using var scope = await _accessLimiter.CanAccessAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        var evidenceIds = message.EvidenceIds;
        var evidenceList = new List<EvidenceBase>();
        foreach (var evidenceId in evidenceIds)
        {
            if (blockchain.Evidence.TryGetValue(evidenceId, out var evidence))
            {
                evidenceList.Add(evidence);
            }
            else if (blockchain.PendingEvidence.TryGetValue(evidenceId, out var pendingEvidence))
            {
                evidenceList.Add(pendingEvidence);
            }

            if (evidenceList.Count == message.ChunkSize)
            {
                var response = new EvidenceResponseMessage
                {
                    Evidence = [.. evidenceList],
                };
                transport.Post(messageEnvelope.Sender, response, messageEnvelope.Identity);
                evidenceList.Clear();
                await Task.Yield();
            }
        }

        var lastResponse = new EvidenceResponseMessage
        {
            Evidence = [.. evidenceList],
            IsLast = true,
        };
        transport.Post(messageEnvelope.Sender, lastResponse, messageEnvelope.Identity);
        await Task.Yield();
    }
}
