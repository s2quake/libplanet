using System.Runtime.CompilerServices;
using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;

namespace Libplanet.Net.Services;

public sealed class EvidenceFetcher(
    Blockchain blockchain, ITransport transport, TimeoutOptions timeoutOptions)
    : FetcherBase<EvidenceId, EvidenceBase>
{
    protected override async IAsyncEnumerable<EvidenceBase> FetchOverrideAsync(
        Peer peer, ImmutableArray<EvidenceId> ids, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new EvidenceRequestMessage { EvidenceIds = ids };
        using var cancellationTokenSource = CreateCancellationTokenSource();
        var response = await transport.SendAsync<EvidenceResponseMessage>(peer, request, cancellationTokenSource.Token);
        // await foreach (var item in transport.SendAsync<EvidenceMessage>(peer, request, cancellationToken))
        {
            for (var i = 0; i < response.Evidence.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return response.Evidence[i];
            }
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

    protected override bool Predicate(EvidenceId id)
    {
        var pendingEvidences = blockchain.PendingEvidences;
        var evidences = blockchain.Evidences;
        return pendingEvidences.ContainsKey(id) && !evidences.ContainsKey(id);
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
