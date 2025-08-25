using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Libplanet.State;
using Libplanet.Data;
using Libplanet.Types;
using System.Reactive.Subjects;

namespace Libplanet;

public sealed class StagedTransactionCollection : IReadOnlyDictionary<TxId, Transaction>
{
    private readonly Subject<Transaction> _addedSubject = new();
    private readonly Subject<Transaction> _removedSubject = new();
    private readonly Repository _repository;
    private readonly PendingTransactionIndex _stagedIndex;
    private readonly CommittedTransactionIndex _committedIndex;
    private readonly ConcurrentDictionary<Address, ImmutableArray<long>> _noncesByAddress = new();

    public StagedTransactionCollection(Repository repository, TransactionOptions options)
    {
        _repository = repository;
        _stagedIndex = repository.PendingTransactions;
        _committedIndex = repository.CommittedTransactions;
        Lifetime = options.LifeTime;

        foreach (var transaction in _stagedIndex.Values)
        {
            var address = transaction.Signer;
            var newNonce = transaction.Nonce;
            _noncesByAddress.AddOrUpdate(
                address,
                address => [newNonce],
                (_, existingNonces) => Insert(existingNonces, newNonce));
        }
    }

    public StagedTransactionCollection(Repository repository)
        : this(repository, new TransactionOptions())
    {
    }

    public IObservable<Transaction> Added => _addedSubject;

    public IObservable<Transaction> Removed => _removedSubject;

    public TimeSpan Lifetime { get; }

    public IEnumerable<TxId> Keys => _stagedIndex.Keys;

    public IEnumerable<Transaction> Values => _stagedIndex.Values;

    public int Count => _stagedIndex.Count;

    public Transaction this[TxId txId] => _stagedIndex[txId];

    public void Add(Transaction transaction)
    {
        if (_committedIndex.ContainsKey(transaction.Id))
        {
            throw new ArgumentException(
                $"Transaction {transaction.Id} already exists in the committed transactions.", nameof(transaction));
        }

        if (_repository.GenesisBlockHash != transaction.GenesisBlockHash)
        {
            throw new ArgumentException(
                $"Transaction {transaction.Id} has a different genesis hash than the repository's genesis block.",
                nameof(transaction));
        }

        // options.Validate(transaction);

        if (!_stagedIndex.TryAdd(transaction))
        {
            throw new ArgumentException(
                $"Transaction {transaction.Id} already exists in the staged transactions.", nameof(transaction));
        }

        AddNonce(transaction);
        _addedSubject.OnNext(transaction);
    }

    public Transaction Add(ISigner signer, TransactionParams @params)
    {
        var tx = new TransactionMetadata
        {
            Nonce = GetNextTxNonce(signer.Address),
            Signer = signer.Address,
            GenesisBlockHash = _repository.GenesisBlockHash,
            Actions = @params.Actions.ToBytecodes(),
            Timestamp = @params.Timestamp == default ? DateTimeOffset.UtcNow : @params.Timestamp,
            MaxGasPrice = @params.MaxGasPrice,
            GasLimit = @params.GasLimit,
        }.Sign(signer);

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
        if (_stagedIndex.TryGetValue(txId, out var transaction))
        {
            _stagedIndex.Remove(txId);
            RemoveNonce(transaction);
            _removedSubject.OnNext(transaction);
            return true;
        }

        return false;
    }

    public bool Remove(Transaction transaction)
    {
        if (_stagedIndex.Remove(transaction.Id))
        {
            RemoveNonce(transaction);
            _removedSubject.OnNext(transaction);
            return true;
        }

        return false;
    }

    public ImmutableSortedSet<Transaction> Collect() => Collect(maxTransactionsPerBlock: 100);

    public ImmutableSortedSet<Transaction> Collect(int maxTransactionsPerBlock)
    {
        var items = Values.Where(item => !IsExpired(item, Lifetime))
            .OrderBy(item => item.Nonce)
            .ThenBy(item => item.Timestamp).ToArray();
        var itemList = new List<Transaction>(items.Length);
        var nonceByAddress = new Dictionary<Address, long>();
        foreach (var item in items)
        {
            var signer = item.Signer;
            var nonce = nonceByAddress.GetValueOrDefault(signer, _repository.GetNonce(signer));
            if (nonce == item.Nonce)
            {
                itemList.Add(item);
                nonceByAddress[signer] = nonce + 1;
            }

            if (itemList.Count >= maxTransactionsPerBlock)
            {
                break;
            }
        }

        return [.. itemList];
    }

    public long GetNextTxNonce(Address address)
    {
        if (_noncesByAddress.TryGetValue(address, out var nonces) && !nonces.IsEmpty)
        {
            var n = nonces[0] + 1;
            for (var i = 1; i < nonces.Length; i++)
            {
                if (n == nonces[i])
                {
                    n++;
                }
                else if (n > nonces[i] + 1)
                {
                    break;
                }
            }

            return n;
        }

        return _repository.GetNonce(address);
    }

    public void Prune()
    {
        var query = from tx in Values
                    where IsExpired(tx, Lifetime) || tx.Nonce < _repository.GetNonce(tx.Signer)
                    select tx;
        var txs = query.ToArray();
        foreach (var tx in txs)
        {
            Remove(tx.Id);
        }
    }

    public bool ContainsKey(TxId txId) => _stagedIndex.ContainsKey(txId);

    public bool TryGetValue(TxId txId, [MaybeNullWhen(false)] out Transaction value)
        => _stagedIndex.TryGetValue(txId, out value);

    IEnumerator<KeyValuePair<TxId, Transaction>> IEnumerable<KeyValuePair<TxId, Transaction>>.GetEnumerator()
    {
        foreach (var kvp in _stagedIndex)
        {
            yield return kvp;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var kvp in _stagedIndex)
        {
            yield return kvp;
        }
    }

    private static bool IsExpired(Transaction transaction, TimeSpan lifetime)
        => transaction.Timestamp + lifetime < DateTimeOffset.UtcNow;

    private bool IsValidNonce(Transaction transaction)
    {
        var address = transaction.Signer;
        var nonce = transaction.Nonce;
        return _noncesByAddress.TryGetValue(address, out var nonces) && nonces.Contains(nonce);
    }

    private void AddNonce(Transaction transaction)
    {
        var address = transaction.Signer;
        var nonce = transaction.Nonce;

        _noncesByAddress.AddOrUpdate(
            address,
            _ => [nonce],
            (_, existingNonces) => Insert(existingNonces, nonce));
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

    private static ImmutableArray<long> Insert(ImmutableArray<long> array, long item)
    {
        var insertPosition = array.Length;
        for (var i = 0; i < array.Length; i++)
        {
            if (item < array[i])
            {
                insertPosition = i;
            }
        }

        return array.Insert(insertPosition, item);
    }
}
