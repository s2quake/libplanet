using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Action;
using Libplanet.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public sealed class StagedTransactionCollection(Repository repository, BlockChainOptions options)
    : IReadOnlyDictionary<TxId, Transaction>
{
    private readonly PendingTransactionStore _store = repository.PendingTransactions;
    private readonly Chain _chain = repository.Chain;

    public StagedTransactionCollection(Repository repository)
        : this(repository, new BlockChainOptions())
    {
    }

    public TimeSpan Lifetime { get; } = options.TransactionOptions.LifeTime;

    public IEnumerable<TxId> Keys => _store.Keys;

    public IEnumerable<Transaction> Values => _store.Values;

    public int Count => _store.Count;

    public Transaction this[TxId txId] => _store[txId];

    public bool TryAdd(Transaction transaction)
    {
        if (transaction.Timestamp + options.TransactionOptions.LifeTime < DateTimeOffset.UtcNow)
        {
            return false;
        }

        // compare with repository genesis

        return _store.TryAdd(transaction);
    }

    public void Add(Transaction transaction)
    {
        if (transaction.Timestamp + options.TransactionOptions.LifeTime < DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Transaction is expired.", nameof(transaction));
        }

        // compare with repository genesis

        if (!_store.TryAdd(transaction))
        {
            throw new ArgumentException(
                $"Transaction {transaction.Id} already exists in the store.", nameof(transaction));
        }
    }

    public Transaction Add(TransactionSubmission submission)
    {
        var tx = new TransactionMetadata
        {
            Nonce = GetNextTxNonce(submission.Signer.Address),
            Signer = submission.Signer.Address,
            GenesisHash = _chain.GenesisBlockHash,
            Actions = submission.Actions.ToBytecodes(),
            Timestamp = submission.Timestamp,
            MaxGasPrice = submission.MaxGasPrice,
            GasLimit = submission.GasLimit,
        }.Sign(submission.Signer);

        Add(tx);
        return tx;
    }

    public bool Remove(TxId txId) => _store.Remove(txId);

    public bool Remove(Transaction transaction) => _store.Remove(transaction.Id);

    public ImmutableSortedSet<Transaction> Collect()
    {
        var blockOptions = options.BlockOptions;
        var items = Values.OrderBy(item => item.Nonce).ThenBy(item => item.Timestamp).ToArray();
        var itemList = new List<Transaction>(items.Length);
        foreach (var item in items)
        {
            if (IsExpired(item, Lifetime))
            {
                Remove(item.Id);
            }
            else
            {
                itemList.Add(item);
            }

            if (itemList.Count >= blockOptions.MaxTransactionsPerBlock)
            {
                break;
            }
        }

        return [.. itemList];
    }

    public long GetNextTxNonce(Address address)
    {
        var nonce = _chain.GetNonce(address);
        var txs = Values
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
