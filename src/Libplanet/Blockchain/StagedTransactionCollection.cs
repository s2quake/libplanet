using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Action;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public sealed class StagedTransactionCollection(Repository repository, BlockChainOptions options)
    : IReadOnlyDictionary<TxId, Transaction>
{
    private readonly PendingTransactionStore _store = repository.PendingTransactions;

    public StagedTransactionCollection(Repository repository)
        : this(repository, new BlockChainOptions())
    {
    }

    public TimeSpan Lifetime => options.TransactionOptions.LifeTime;

    public IEnumerable<TxId> Keys => _store.Keys;

    public IEnumerable<Transaction> Values => _store.Values;

    public int Count => _store.Count;

    public Transaction this[TxId txId] => _store[txId];

    public bool Add(Transaction transaction)
    {
        if (transaction.Timestamp + options.TransactionOptions.LifeTime < DateTimeOffset.UtcNow)
        {
            return false;
        }

        // compare with repository genesis

        return _store.TryAdd(transaction);
    }

    public bool Remove(TxId txId) => _store.Remove(txId);

    internal bool Ignore(TxId txId) => _store.Remove(txId);

    internal ImmutableList<Transaction> ListStagedTransactions()
    {
        var unorderedTxs = Iterate();
        Transaction[] txs = unorderedTxs.ToArray();

        Dictionary<Address, LinkedList<Transaction>> seats = txs
            .GroupBy(tx => tx.Signer)
            .Select(g => (g.Key, new LinkedList<Transaction>(g.OrderBy(tx => tx.Nonce))))
            .ToDictionary(pair => pair.Key, pair => pair.Item2);

        return txs.Select(tx =>
        {
            LinkedList<Transaction> seat = seats[tx.Signer];
            Transaction first = seat.First.Value;
            seat.RemoveFirst();
            return first;
        }).ToImmutableList();
    }

    public ImmutableSortedSet<Transaction> Collect()
    {
        // var index = Blocks.Count;
        var blockOptions = options.BlockOptions;
        ImmutableList<Transaction> stagedTransactions = ListStagedTransactions();

        var transactions = new List<Transaction>();

        // FIXME: The tx collection timeout should be configurable.
        DateTimeOffset timeout = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(4);

        var estimatedEncoding = 0L;
        var storedNonces = new Dictionary<Address, long>();
        var nextNonces = new Dictionary<Address, long>();
        var toProposeCounts = new Dictionary<Address, int>();

        foreach (
            (Transaction tx, int i) in stagedTransactions.Select((val, idx) => (val, idx)))
        {
            // We don't care about nonce ordering here because `.ListStagedTransactions()`
            // returns already ordered transactions by its nonce.
            if (!storedNonces.ContainsKey(tx.Signer))
            {
                storedNonces[tx.Signer] = GetNextTxNonce(tx.Signer);
                nextNonces[tx.Signer] = storedNonces[tx.Signer];
                toProposeCounts[tx.Signer] = 0;
            }

            if (transactions.Count >= blockOptions.MaxTransactionsPerBlock)
            {
                break;
            }

            if (storedNonces[tx.Signer] <= tx.Nonce && tx.Nonce == nextNonces[tx.Signer])
            {
                try
                {
                    options.TransactionOptions.Validate(tx);
                }
                catch
                {
                    Ignore(tx.Id);
                    continue;
                }

                var txAddedEncoding = estimatedEncoding + ModelSerializer.SerializeToBytes(tx).Length;
                if (txAddedEncoding > blockOptions.MaxTransactionsBytes)
                {
                    continue;
                }
                else if (toProposeCounts[tx.Signer] >= blockOptions.MaxTransactionsPerSignerPerBlock)
                {
                    continue;
                }

                try
                {
                    _ = tx.Actions.Select(item => item.ToAction<IAction>());
                }
                catch (Exception e)
                {
                    continue;
                }

                transactions.Add(tx);
                nextNonces[tx.Signer] += 1;
                toProposeCounts[tx.Signer] += 1;
                estimatedEncoding = txAddedEncoding;
            }
            else if (tx.Nonce < storedNonces[tx.Signer])
            {
            }
            else
            {
            }

            if (timeout < DateTimeOffset.UtcNow)
            {
                break;
            }
        }

        if (transactions.Count < blockOptions.MinTransactionsPerBlock)
        {
            throw new InvalidOperationException(
                $"Only gathered {transactions.Count} transactions where " +
                $"the minimal number of transactions to propose is {blockOptions.MinTransactionsPerBlock}.");
        }

        return transactions.ToImmutableSortedSet();
    }

    public ImmutableArray<Transaction> Iterate(bool filtered = true)
    {
        var query = from item in _store.Values
                    where !filtered || !IsExpired(item, options.TransactionOptions.LifeTime)
                    select item;

        return [.. query];
    }

    public long GetNextTxNonce(Address address)
    {
        var nonce = repository.GetNonce(address);
        var txs = Iterate(filtered: true)
            .Where(tx => tx.Signer.Equals(address))
            .OrderBy(tx => tx.Nonce);

        foreach (var tx in txs)
        {
            if (nonce < tx.Nonce)
            {
                break;
            }
            else if (nonce == tx.Nonce)
            {
                nonce++;
            }
        }

        return nonce;
    }

    private static bool IsExpired(Transaction transaction, TimeSpan lifetime)
    {
        return transaction.Timestamp + lifetime < DateTimeOffset.UtcNow;
    }

    public bool ContainsKey(TxId txId) => _store.ContainsKey(txId);

    public bool TryGetValue(TxId txId, [MaybeNullWhen(false)] out Transaction value)
        => _store.TryGetValue(txId, out value);

    IEnumerator<KeyValuePair<TxId, Transaction>> IEnumerable<KeyValuePair<TxId, Transaction>>.GetEnumerator()
    {
        foreach (var kvp in _store)
        {
            yield return kvp;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var kvp in _store)
        {
            yield return kvp;
        }
    }
}
