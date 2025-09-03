using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Libplanet.State;
using Libplanet.Data;
using Libplanet.Types;
using System.Reactive.Subjects;
using Libplanet.Serialization;
using Libplanet.Types.Threading;

namespace Libplanet;

public sealed class StagedTransactionCollection : IReadOnlyDictionary<TxId, Transaction>
{
    private readonly Subject<Transaction> _addedSubject = new();
    private readonly Subject<Transaction> _removedSubject = new();
    private readonly Repository _repository;
    private readonly PendingTransactionIndex _stagedIndex;
    private readonly CommittedTransactionIndex _committedIndex;
    private readonly BlockchainOptions _options;
    private readonly Dictionary<Address, ImmutableArray<long>> _noncesByAddress;

    private readonly Dictionary<TxId, bool> _isActionValidById = [];
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    public StagedTransactionCollection(Repository repository, BlockchainOptions options)
    {
        _repository = repository;
        _stagedIndex = repository.PendingTransactions;
        _committedIndex = repository.CommittedTransactions;
        _options = options;
        _noncesByAddress = Create(repository.PendingTransactions);
        _stagedIndex.Added += (sender, tx) =>
        {
            AddNonce(tx);
            _addedSubject.OnNext(tx);
        };
        _stagedIndex.Removed += (sender, tx) =>
        {
            RemoveNonce(tx);
            _isActionValidById.Remove(tx.Id);
            _removedSubject.OnNext(tx);
        };
    }

    public StagedTransactionCollection(Repository repository)
        : this(repository, new BlockchainOptions())
    {
    }

    public IObservable<Transaction> Added => _addedSubject;

    public IObservable<Transaction> Removed => _removedSubject;

    public IEnumerable<TxId> Keys
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _stagedIndex.Keys;
        }
    }

    public IEnumerable<Transaction> Values
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _stagedIndex.Values;
        }
    }

    public int Count
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _stagedIndex.Count;
        }
    }

    public Transaction this[TxId txId]
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _stagedIndex[txId];
        }
    }

    public void Add(Transaction transaction)
    {
        using var _ = _lock.WriteScope();
        AddInternal(transaction);
    }

    public Transaction Add(ISigner signer, TransactionParams @params)
    {
        using var _ = _lock.WriteScope();
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

        AddInternal(tx);
        return tx;
    }

    public void AddRange(IEnumerable<Transaction> transactions)
    {
        using var _ = _lock.WriteScope();
        foreach (var transaction in transactions)
        {
            AddInternal(transaction);
        }
    }

    public bool Remove(TxId txId)
    {
        using var _ = _lock.WriteScope();
        return RemoveInternal(txId);
    }

    public bool Remove(Transaction transaction)
    {
        using var _ = _lock.WriteScope();
        return RemoveInternal(transaction.Id);
    }

    public ImmutableArray<Transaction> Collect() => Collect(DateTimeOffset.UtcNow);

    public ImmutableArray<Transaction> Collect(DateTimeOffset timestamp)
    {
        using var _ = _lock.ReadScope();
        var maxTransactions = _options.BlockOptions.MaxTransactions;
        var maxActionBytes = _options.BlockOptions.MaxActionBytes;
        var maxTransactionsPerSigner = _options.BlockOptions.MaxTransactionsPerSigner;
        var sorter = _options.TransactionOptions.Sorter;
        var items1 = Values.Where(item => !IsExpired(item, timestamp) && IsValid(item));
        var items2 = sorter(items1);

        var nonceByAddress = new Dictionary<Address, long>();
        var countBySigner = new Dictionary<Address, int>();
        var txList = new List<Transaction>(items2.Count());
        var totalActionBytes = 0L;
        foreach (var item in items2)
        {
            var signer = item.Signer;
            var nonce = nonceByAddress.GetValueOrDefault(signer, _repository.GetNonce(signer));
            var actionBytes = item.Actions.Aggregate(0L, (s, i) => s + i.Bytes.Length);

            if (nonce == item.Nonce)
            {
                if (totalActionBytes + actionBytes > maxActionBytes)
                {
                    break;
                }

                var count = countBySigner.GetValueOrDefault(signer, 0);
                if (count >= maxTransactionsPerSigner)
                {
                    continue;
                }

                txList.Add(item);
                nonceByAddress[signer] = nonce + 1;
                totalActionBytes += actionBytes;
                countBySigner[signer] = count + 1;
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
        using var _ = _lock.ReadScope();
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

    public void Prune() => Prune(_repository.Timestamp);

    public void Prune(DateTimeOffset timestamp)
    {
        using var _ = _lock.WriteScope();
        var query = from tx in Values
                    where IsExpired(tx, timestamp) || !IsValid(tx)
                    select tx;
        var txs = query.ToArray();
        foreach (var tx in txs)
        {
            RemoveInternal(tx.Id);
        }
    }

    public bool ContainsKey(TxId txId)
    {
        using var _ = _lock.ReadScope();
        return _stagedIndex.ContainsKey(txId);
    }

    public bool TryGetValue(TxId txId, [MaybeNullWhen(false)] out Transaction value)
    {
        using var _ = _lock.ReadScope();
        return _stagedIndex.TryGetValue(txId, out value);
    }

    public bool IsValid(Transaction transaction)
    {
        try
        {
            if (transaction.Nonce >= _repository.GetNonce(transaction.Signer))
            {
                if (!_isActionValidById.TryGetValue(transaction.Id, out var isValid))
                {
                    try
                    {
                        Parallel.ForEach(
                            transaction.Actions,
                            item => _ = ModelSerializer.DeserializeFromBytes<IAction>(item.Bytes.AsSpan()));
                        isValid = true;
                    }
                    catch
                    {
                        isValid = false;
                    }

                    _isActionValidById[transaction.Id] = isValid;
                }

                if (isValid)
                {
                    _options.TransactionOptions.Validate(transaction);
                    return true;
                }
            }
        }
        catch
        {
            // do nothing
        }

        return false;
    }

    public IEnumerator<KeyValuePair<TxId, Transaction>> GetEnumerator()
    {
        using var _ = _lock.ReadScope();
        foreach (var kvp in _stagedIndex.ToArray())
        {
            yield return kvp;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

    private static Dictionary<Address, ImmutableArray<long>> Create(PendingTransactionIndex pendingIndex)
    {
        var noncesByAddress = new Dictionary<Address, ImmutableArray<long>>();
        foreach (var transaction in pendingIndex.Values)
        {
            var address = transaction.Signer;
            var newNonce = transaction.Nonce;
            if (noncesByAddress.TryGetValue(address, out var existingNonces))
            {
                noncesByAddress[address] = Insert(existingNonces, newNonce);
            }
            else
            {
                noncesByAddress[address] = [newNonce];
            }
        }

        return noncesByAddress;
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

    private void AddInternal(Transaction transaction)
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

        // AddNonce(transaction);
        // _addedSubject.OnNext(transaction);
    }

    private bool RemoveInternal(TxId txId)
    {
        if (_stagedIndex.TryGetValue(txId, out _))
        {
            _stagedIndex.Remove(txId);
            // RemoveNonce(transaction);
            // _isActionValidById.Remove(txId);
            // _removedSubject.OnNext(transaction);
            return true;
        }

        return false;
    }

    private bool IsExpired(Transaction transaction, DateTimeOffset timestamp)
        => transaction.Timestamp + _options.TransactionOptions.Lifetime < timestamp;

    private void AddNonce(Transaction transaction)
    {
        var address = transaction.Signer;
        var nonce = transaction.Nonce;

        if (_noncesByAddress.TryGetValue(address, out var existingNonces))
        {
            _noncesByAddress[address] = Insert(existingNonces, nonce);
        }
        else
        {
            _noncesByAddress[address] = [nonce];
        }
    }

    private void RemoveNonce(Transaction transaction)
    {
        var address = transaction.Signer;
        var nonce = transaction.Nonce;
        _noncesByAddress[address] = _noncesByAddress[address].Remove(nonce);
    }
}
