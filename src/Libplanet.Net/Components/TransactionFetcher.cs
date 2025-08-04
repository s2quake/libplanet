using System.Runtime.CompilerServices;
using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Components;

public sealed class TransactionFetcher(Blockchain blockchain, ITransport transport)
    : FetcherBase<TxId, Transaction>
{
    private readonly TransactionCollection _transactions = blockchain.Transactions;
    private readonly StagedTransactionCollection _stagedTransactions = blockchain.StagedTransactions;

    protected override async IAsyncEnumerable<Transaction> FetchOverrideAsync(
        Peer peer, ImmutableArray<TxId> ids, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new TransactionRequestMessage { TxIds = ids };
        var isLast = new Func<TransactionResponseMessage, bool>(m => m.IsLast);
        var query = transport.SendAsync(peer, request, isLast, cancellationToken);
        await foreach (var item in query)
        {
            foreach (var transaction in item.Transactions)
            {
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
        => !_transactions.ContainsKey(item.Id) && !_stagedTransactions.ContainsKey(item.Id);
}
