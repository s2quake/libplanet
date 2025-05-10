using System.Collections;
using System.Collections.Concurrent;
using Libplanet.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public sealed class StagedTransactionCollection(Libplanet.Store.Store store, Guid blockChainId, TimeSpan lifetime)
    : IEnumerable<StagedTransaction>
{
    private readonly ConcurrentDictionary<TxId, StagedTransaction> _staged = new();

    public StagedTransactionCollection(Libplanet.Store.Store store, Guid blockChainId)
        : this(store, blockChainId, TimeSpan.FromSeconds(10 * 60))
    {
    }

    public TimeSpan Lifetime => lifetime;

    public bool Stage(Transaction transaction) => _staged.TryAdd(transaction.Id, new StagedTransaction
    {
        Transaction = transaction,
        Lifetime = DateTimeOffset.UtcNow + lifetime,
    });

    public bool Unstage(TxId txId) => _staged.TryRemove(txId, out _);

    public bool Ignore(TxId txId)
    {
        if (_staged.TryGetValue(txId, out var item) && !item.IsIgnored)
        {
            _staged.TryUpdate(txId, item with { IsIgnored = true }, item);
            return true;
        }

        return false;
    }

    public bool Ignores(TxId txId) => _staged.TryGetValue(txId, out var item) && item.IsIgnored;

    public Transaction Get(TxId txId, bool filtered = true)
    {
        if (_staged.TryGetValue(txId, out var item))
        {
            if (!filtered || item.IsEnabled(store, blockChainId))
            {
                return item.Transaction;
            }

            throw new InvalidOperationException($"Transaction {txId} is ignored or expired.");
        }

        throw new InvalidOperationException($"Transaction {txId} not found in the stage.");
    }

    public ImmutableArray<Transaction> Iterate(bool filtered = true)
    {
        var query = from item in _staged.Values
                    where !filtered || item.IsEnabled(store, blockChainId)
                    select item.Transaction;

        return [.. query];
    }

    public long GetNextTxNonce(Address address)
    {
        var nonce = store.GetTxNonce(blockChainId, address);
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

    IEnumerator<StagedTransaction> IEnumerable<StagedTransaction>.GetEnumerator() => _staged.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _staged.Values.GetEnumerator();
}
