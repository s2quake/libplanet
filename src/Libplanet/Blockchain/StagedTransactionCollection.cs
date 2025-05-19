using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public sealed class StagedTransactionCollection(Repository repository, TimeSpan lifetime)
    : IReadOnlyDictionary<TxId, Transaction>
{
    private readonly PendingTransactionStore _store = repository.PendingTransactions;

    public StagedTransactionCollection(Repository repository)
        : this(repository, TimeSpan.FromSeconds(10 * 60))
    {
    }

    public TimeSpan Lifetime => lifetime;

    public IEnumerable<TxId> Keys => _store.Keys;

    public IEnumerable<Transaction> Values => _store.Values;

    public int Count => _store.Count;

    public Transaction this[TxId txId] => _store[txId];

    public bool Stage(Transaction transaction)
    {
        if (transaction.Timestamp + lifetime < DateTimeOffset.UtcNow)
        {
            return false;
        }

        return _store.TryAdd(transaction);
    }

    public bool Unstage(TxId txId) => _store.Remove(txId);

    internal bool Ignore(TxId txId) => _store.Remove(txId);

    // public bool Ignores(TxId txId) => _staged.TryGetValue(txId, out var item) && item.IsIgnored;

    // public Transaction Get(TxId txId, bool filtered = true)
    // {
    //     if (_staged.TryGetValue(txId, out var item))
    //     {
    //         if (!filtered || item.IsEnabled(repository, blockChainId))
    //         {
    //             return item.Transaction;
    //         }

    //         throw new InvalidOperationException($"Transaction {txId} is ignored or expired.");
    //     }

    //     throw new InvalidOperationException($"Transaction {txId} not found in the stage.");
    // }

    public ImmutableArray<Transaction> Iterate(bool filtered = true)
    {
        var query = from item in _store.Values
                    where !filtered || !IsExpired(item, lifetime)
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
