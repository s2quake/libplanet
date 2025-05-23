using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Data;
using Libplanet.Types.Transactions;

namespace Libplanet.Blockchain;

public sealed class TransactionCollection(Repository repository)
    : IReadOnlyDictionary<TxId, Transaction>
{
    private readonly CommittedTransactionStore _store = repository.CommittedTransactions;

    public IEnumerable<TxId> Keys => _store.Keys;

    public IEnumerable<Transaction> Values => _store.Values;

    public int Count => _store.Count;

    public Transaction this[TxId txId] => _store[txId];

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
