using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
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
        var count = ids.Length;

        var txRecvTimeout = timeoutOptions.GetTxsBaseTimeout + timeoutOptions.GetTxsPerTxIdTimeout.Multiply(count);
        if (txRecvTimeout > timeoutOptions.MaxTimeout)
        {
            txRecvTimeout = timeoutOptions.MaxTimeout;
        }

        IEnumerable<MessageEnvelope> replies;
        try
        {
            replies = await transport.SendMessageAsync(
                peer,
                request,
                count,
                cancellationToken)
            .ConfigureAwait(false);
        }
        catch (CommunicationException e) when (e.InnerException is TimeoutException)
        {
            yield break;
        }

        foreach (MessageEnvelope message in replies)
        {
            if (message.Message is EvidenceMessage parsed)
            {
                yield return ModelSerializer.DeserializeFromBytes<EvidenceBase>(parsed.Payload.AsSpan());
            }
            else
            {
                string errorMessage =
                    $"Expected {nameof(Transaction)} messages as response of " +
                    $"the {nameof(GetTransactionMessage)} message, but got a {message.GetType().Name} " +
                    $"message instead: {message}";
                throw new InvalidMessageContractException(errorMessage);
            }
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
