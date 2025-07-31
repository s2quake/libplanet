using System.Runtime.CompilerServices;
using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Components;

public sealed class EvidenceFetcher(Blockchain blockchain, ITransport transport)
    : FetcherBase<EvidenceId, EvidenceBase>
{
    protected override async IAsyncEnumerable<EvidenceBase> FetchOverrideAsync(
        Peer peer, ImmutableArray<EvidenceId> ids, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new EvidenceRequestMessage { EvidenceIds = ids };
        var isLast = new Func<EvidenceResponseMessage, bool>(m => m.IsLast);
        var query = transport.SendAsync(peer, request, isLast, cancellationToken);
        await foreach (var item in query)
        {
            foreach (var evidence in item.Evidence)
            {
                yield return evidence;
            }
        }
    }

    protected override bool Predicate(EvidenceId id)
    {
        var pendingEvidence = blockchain.PendingEvidence;
        var evidence = blockchain.Evidence;
        return !pendingEvidence.ContainsKey(id) && !evidence.ContainsKey(id);
    }

    protected override bool Verify(EvidenceBase item)
    {
        var pendingEvidence = blockchain.PendingEvidence;
        var evidence = blockchain.Evidence;

        try
        {
            return !evidence.ContainsKey(item.Id) && !pendingEvidence.ContainsKey(item.Id);
        }
        catch
        {
            return false;
        }
    }
}
