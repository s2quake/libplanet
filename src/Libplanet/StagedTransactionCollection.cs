using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Libplanet.State;
using Libplanet.Data;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;

namespace Libplanet;

public sealed class StagedTransactionCollection(Repository repository, BlockchainOptions options)
    : IReadOnlyDictionary<TxId, Transaction>
{
    private readonly PendingTransactionStore _store = repository.PendingTransactions;
    private readonly ConcurrentDictionary<Address, ImmutableSortedSet<long>> _noncesByAddress = new();

    public StagedTransactionCollection(Repository repository)
        : this(repository, new BlockchainOptions())
    {
        foreach (var transaction in _store.Values)
        {
            var address = transaction.Signer;
            var newNonce = transaction.Nonce;
            _noncesByAddress.AddOrUpdate(
                address,
                address => [newNonce],
                (_, existingNonces) => existingNonces.Add(newNonce)
            );
        }
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

        if (_store.TryAdd(transaction))
        {
            AddNonce(transaction);
            return true;
        }

        return false;
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

        AddNonce(transaction);
    }

    public Transaction Add(TransactionSubmission submission)
    {
        var tx = new TransactionMetadata
        {
            Nonce = GetNextTxNonce(submission.Signer.Address),
            Signer = submission.Signer.Address,
            GenesisHash = repository.GenesisBlockHash,
            Actions = submission.Actions.ToBytecodes(),
            Timestamp = submission.Timestamp,
            MaxGasPrice = submission.MaxGasPrice,
            GasLimit = submission.GasLimit,
        }.Sign(submission.Signer);

        Add(tx);
        return tx;
    }

    public void AddRange(IEnumerable<Transaction> transactions)
    {
        foreach (var transaction in transactions)
        {
            Add(transaction);
        }
    }

    public bool Remove(TxId txId)
    {
        if (_store.TryGetValue(txId, out var transaction))
        {
            _store.Remove(txId);
            RemoveNonce(transaction);
            return true;
        }

        return false;
    }

    public bool Remove(Transaction transaction)
    {
        if (_store.Remove(transaction.Id))
        {
            RemoveNonce(transaction);
            return true;
        }

        return false;
    }

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
        => _noncesByAddress.TryGetValue(address, out var nonces) ? nonces.Max + 1 : repository.GetNonce(address);

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

    private void AddNonce(Transaction transaction)
    {
        var address = transaction.Signer;
        var nonce = transaction.Nonce;
        _noncesByAddress.AddOrUpdate(
            address,
            _ => [nonce],
            (_, existingNonces) => existingNonces.Add(nonce)
        );
    }

    private void RemoveNonce(Transaction transaction)
    {
        var address = transaction.Signer;
        var nonce = transaction.Nonce;
        _noncesByAddress.AddOrUpdate(
            address,
            _ => [],
            (_, existingNonces) => existingNonces.Remove(nonce)
        );
    }
}
