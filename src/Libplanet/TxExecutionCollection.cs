using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet;

public sealed class TxExecutionCollection(Repository repository)
    : IReadOnlyDictionary<TxId, TransactionExecutionInfo>
{
    private readonly TxExecutionIndex _store = repository.TxExecutions;

    public IEnumerable<TxId> Keys => _store.Keys;

    public IEnumerable<TransactionExecutionInfo> Values => _store.Values;

    public int Count => _store.Count;

    public TransactionExecutionInfo this[TxId txId] => _store[txId];

    public bool ContainsKey(TxId txId) => _store.ContainsKey(txId);

    public bool TryGetValue(TxId txId, [MaybeNullWhen(false)] out TransactionExecutionInfo value)
        => _store.TryGetValue(txId, out value);

    IEnumerator<KeyValuePair<TxId, TransactionExecutionInfo>> IEnumerable<KeyValuePair<TxId, TransactionExecutionInfo>>.GetEnumerator()
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
