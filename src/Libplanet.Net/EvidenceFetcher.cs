using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net;

public sealed class EvidenceFetcher(
    Blockchain blockchain, ITransport transport, TimeoutOptions timeoutOptions)
    : FetcherBase<EvidenceId, EvidenceBase>
{
    protected override async IAsyncEnumerable<EvidenceBase> FetchAsync(
        Peer peer, EvidenceId[] ids, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new GetEvidenceMessage { EvidenceIds = [.. ids] };
        using var cancellationTokenSource = CreateCancellationTokenSource();
        await foreach (var evidenceMessage in transport.SendAsync<EvidenceMessage>(peer, request, cancellationToken))
        {
            yield return ModelSerializer.DeserializeFromBytes<EvidenceBase>(evidenceMessage.Payload.AsSpan());
        }

        CancellationTokenSource CreateCancellationTokenSource()
        {
            var count = ids.Length;
            var fetchTimeout = timeoutOptions.GetEvidenceFetchTimeout(count);
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationTokenSource.CancelAfter(fetchTimeout);
            return cancellationTokenSource;
        }
    }

    protected override HashSet<EvidenceId> GetRequiredIds(IEnumerable<EvidenceId> ids)
    {
        var pendingEvidences = blockchain.PendingEvidences;
        var evidences = blockchain.Evidences;
        var query = from id in ids
                    where pendingEvidences.ContainsKey(id) && !evidences.ContainsKey(id)
                    select id;

        return [.. query];
    }

    protected override bool Verify(EvidenceBase item)
    {
        var pendingEvidence = blockchain.PendingEvidences;

        try
        {
            if (!pendingEvidence.ContainsKey(item.Id))
            {
                pendingEvidence.Add(item);
                return true;
            }
        }
        catch
        {
            // do nothing
        }

        return false;
    }
}
