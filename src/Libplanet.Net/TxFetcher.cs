using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net;

public sealed class TxFetcher(
    Blockchain blockchain, ITransport transport, TimeoutOptions timeoutOptions)
    : FetcherBase<TxId, Transaction>
{
    protected override async IAsyncEnumerable<Transaction> FetchAsync(
        Peer peer, TxId[] ids, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new GetTransactionMessage { TxIds = [.. ids] };
        using var cancellationTokenSource = CreateCancellationTokenSource();
        await foreach (var item in transport.SendAsync<TransactionMessage>(peer, request, cancellationToken))
        {
            for (var i = 0; i < item.Transactions.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item.Transactions[i];
            }
        }

        CancellationTokenSource CreateCancellationTokenSource()
        {
            var count = ids.Length;
            var fetchTimeout = timeoutOptions.GetTxFetchTimeout(count);
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationTokenSource.CancelAfter(fetchTimeout);
            return cancellationTokenSource;
        }
    }

    protected override HashSet<TxId> GetRequiredIds(IEnumerable<TxId> ids)
    {
        var stageTransactions = blockchain.StagedTransactions;
        var transactions = blockchain.Transactions;
        var query = from id in ids
                    where stageTransactions.ContainsKey(id) && !transactions.ContainsKey(id)
                    select id;

        return [.. query];
    }

    protected override bool Verify(Transaction item)
    {
        var transactionOptions = blockchain.Options.TransactionOptions;
        var stageTransactions = blockchain.StagedTransactions;

        try
        {
            transactionOptions.Validate(item);
            if (!stageTransactions.ContainsKey(item.Id))
            {
                stageTransactions.Add(item);
                return true;
            }
        }
        catch
        {
            stageTransactions.Remove(item.Id);
        }

        return false;
    }
}
