using System.Collections.Concurrent;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain.Policies;

public sealed class VolatileStagePolicy(TimeSpan lifetime) : IStagePolicy
{
    private readonly ConcurrentDictionary<TxId, Item> _staged = new();

    public VolatileStagePolicy()
        : this(TimeSpan.FromSeconds(10 * 60))
    {
    }

    public TimeSpan Lifetime => lifetime;

    public bool Stage(Transaction transaction)
        => _staged.TryAdd(transaction.Id, new Item(transaction, false, DateTimeOffset.UtcNow + lifetime));

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

    public Transaction Get(BlockChain blockChain, TxId txId, bool filtered = true)
    {
        if (_staged.TryGetValue(txId, out var item))
        {
            if (!filtered || item.IsEnabled(blockChain))
            {
                return item.Transaction;
            }

            throw new InvalidOperationException($"Transaction {txId} is ignored or expired.");
        }

        throw new InvalidOperationException($"Transaction {txId} not found in the stage.");
    }

    public ImmutableArray<Transaction> Iterate(BlockChain blockChain, bool filtered = true)
    {
        var query = from item in _staged.Values
                    where !filtered || item.IsEnabled(blockChain)
                    select item.Transaction;

        return [.. query];
    }

    public long GetNextTxNonce(BlockChain blockChain, Address address)
    {
        var nonce = blockChain.Store.GetTxNonce(blockChain.Id, address);
        var txs = Iterate(blockChain, filtered: true)
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

    private sealed record class Item(Transaction Transaction, bool IsIgnored, DateTimeOffset Lifetime)
    {
        public bool IsEnabled(BlockChain blockChain)
        {
            if (Lifetime > DateTimeOffset.UtcNow)
            {
                return false;
            }

            if (blockChain.Store.GetTxNonce(blockChain.Id, Transaction.Signer) < Transaction.Nonce)
            {
                return false;
            }

            return !IsIgnored;
        }
    }
}
