using System.Runtime.CompilerServices;
using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;

namespace Libplanet.Net;

public sealed class TxFetcher(
    Blockchain blockchain, ITransport transport, TimeoutOptions timeoutOptions)
    : FetcherBase<TxId, Transaction>
{
    public TxFetcher(Swarm swarm, SwarmOptions options)
        : this(swarm.Blockchain, swarm.Transport, options.TimeoutOptions)
    {
    }

    public override async IAsyncEnumerable<Transaction> FetchAsync(
        Peer peer, TxId[] ids, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource();
        var request = new TransactionRequestMessage { TxIds = [.. ids] };
        var response = await transport.SendAsync<TransactionResponseMessage>(peer, request, cancellationTokenSource.Token);
        foreach (var item in response.Transactions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
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
