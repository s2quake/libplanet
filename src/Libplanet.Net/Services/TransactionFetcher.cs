using System.Runtime.CompilerServices;
using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;

namespace Libplanet.Net.Services;

public sealed class TransactionFetcher(
    Blockchain blockchain, ITransport transport, TimeoutOptions timeoutOptions)
    : FetcherBase<TxId, Transaction>
{
    public TransactionFetcher(Swarm swarm, SwarmOptions options)
        : this(swarm.Blockchain, swarm.Transport, options.TimeoutOptions)
    {
    }

    protected override async IAsyncEnumerable<Transaction> FetchOverrideAsync(
        Peer peer, ImmutableArray<TxId> ids, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        var request = new TransactionRequestMessage { TxIds = ids };
        var isLast = new Func<TransactionResponseMessage, bool>(m => m.IsLast);
        var query = transport.SendAsync(peer, request, isLast, cancellationTokenSource.Token);
        await foreach (var item in query)
        {
            foreach (var transaction in item.Transactions)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                yield return transaction;
            }
        }
    }

    protected override bool Predicate(TxId id)
    {
        var stageTransactions = blockchain.StagedTransactions;
        var transactions = blockchain.Transactions;
        return !stageTransactions.ContainsKey(id) && !transactions.ContainsKey(id);
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
