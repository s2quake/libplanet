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
    private readonly BlockchainOptions _options;
    private readonly ConcurrentDictionary<Address, ImmutableArray<long>> _noncesByAddress = new();

    public StagedTransactionCollection(Repository repository, BlockchainOptions options)
    {
        _repository = repository;
        _stagedIndex = repository.PendingTransactions;
        _committedIndex = repository.CommittedTransactions;
        _options = options;

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
        : this(repository, new BlockchainOptions())
    {
    }

    public IObservable<Transaction> Added => _addedSubject;

    public IObservable<Transaction> Removed => _removedSubject;

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
            Nonce = @params.Nonce == -1L ? GetNextTxNonce(signer.Address) : @params.Nonce,
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

    public ImmutableArray<Transaction> Collect() => Collect(DateTimeOffset.UtcNow);

    public ImmutableArray<Transaction> Collect(DateTimeOffset timestamp)
    {
        var lifetime = _options.TransactionOptions.Lifetime;
        var maxTransactions = _options.BlockOptions.MaxTransactions;
        var sorter = _options.TransactionOptions.Sorter;
        var items1 = Values.Where(item => !IsExpired(item, timestamp, lifetime));
        var items2 = sorter(items1);

        var nonceByAddress = new Dictionary<Address, long>();
        var txList = new List<Transaction>(items2.Count());
        foreach (var item in items2)
        {
            var signer = item.Signer;
            var nonce = nonceByAddress.GetValueOrDefault(signer, _repository.GetNonce(signer));
            if (nonce == item.Nonce)
            {
                txList.Add(item);
                nonceByAddress[signer] = nonce + 1;
            }

            if (txList.Count >= maxTransactions)
            {
                break;
            }
        }

        return [.. txList];
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

    public void Prune() => Prune(DateTimeOffset.UtcNow);

    public void Prune(DateTimeOffset timestamp)
    {
        var lifetime = _options.TransactionOptions.Lifetime;
        var query = from tx in Values
                    where IsExpired(tx, timestamp, lifetime) || tx.Nonce < _repository.GetNonce(tx.Signer)
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

    internal static IEnumerable<Transaction> Sort(IEnumerable<Transaction> transactions)
    {
        var list = transactions
            .GroupBy(tx => tx.Signer)
            .Select(group => group.OrderBy(item => item.Nonce).ToList())
            .ToList();

        while (list.Count > 0)
        {
            var first = list[0];
            var item = first[0];
            first.RemoveAt(0);
            list.RemoveAt(0);

            if (first.Count > 0)
            {
                list.Add(first);
            }

            yield return item;
        }
    }

    private static bool IsExpired(Transaction transaction, DateTimeOffset timestamp, TimeSpan lifetime)
        => transaction.Timestamp + lifetime < timestamp;

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
