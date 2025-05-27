using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Data;
using Libplanet.Types.Transactions;

namespace Libplanet;

public sealed class TxExecutionCollection(Repository repository)
    : IReadOnlyDictionary<TxId, TxExecution>
{
    private readonly TxExecutionIndex _store = repository.TxExecutions;

    public IEnumerable<TxId> Keys => _store.Keys;

    public IEnumerable<TxExecution> Values => _store.Values;

    public int Count => _store.Count;

    public TxExecution this[TxId txId] => _store[txId];

    public bool ContainsKey(TxId txId) => _store.ContainsKey(txId);

    public bool TryGetValue(TxId txId, [MaybeNullWhen(false)] out TxExecution value)
        => _store.TryGetValue(txId, out value);

    IEnumerator<KeyValuePair<TxId, TxExecution>> IEnumerable<KeyValuePair<TxId, TxExecution>>.GetEnumerator()
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
