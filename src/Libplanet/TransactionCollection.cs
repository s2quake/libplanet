using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet;

public sealed class TransactionCollection(Repository repository)
    : IReadOnlyDictionary<TxId, Transaction>
{
    private readonly CommittedTransactionIndex _index = repository.CommittedTransactions;

    public IEnumerable<TxId> Keys => _index.Keys;

    public IEnumerable<Transaction> Values => _index.Values;

    public int Count => _index.Count;

    public Transaction this[TxId txId] => _index[txId];

    public bool ContainsKey(TxId txId) => _index.ContainsKey(txId);

    public bool TryGetValue(TxId txId, [MaybeNullWhen(false)] out Transaction value)
        => _index.TryGetValue(txId, out value);

    IEnumerator<KeyValuePair<TxId, Transaction>> IEnumerable<KeyValuePair<TxId, Transaction>>.GetEnumerator()
    {
        foreach (var kvp in _index)
        {
            yield return kvp;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var kvp in _index)
        {
            yield return kvp;
        }
    }
}
