using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Data;
using Libplanet.Types.Evidence;

namespace Libplanet;

public sealed class PendingEvidenceCollection(Repository repository)
    : IReadOnlyDictionary<EvidenceId, EvidenceBase>
{
    private readonly PendingEvidenceStore _store = repository.PendingEvidences;

    public IEnumerable<EvidenceId> Keys => _store.Keys;

    public IEnumerable<EvidenceBase> Values => _store.Values;

    public int Count => _store.Count;

    public EvidenceBase this[EvidenceId txId] => _store[txId];

    public void Add(EvidenceBase evidence)
    {
        if (!_store.TryAdd(evidence))
        {
            throw new ArgumentException(
                $"Evidence with ID {evidence.Id} already exists in the collection.",
                nameof(evidence));
        }
    }

    public bool Remove(EvidenceId txId) => _store.Remove(txId);

    public bool ContainsKey(EvidenceId evidenceId) => _store.ContainsKey(evidenceId);

    public bool TryGetValue(EvidenceId txId, [MaybeNullWhen(false)] out EvidenceBase value)
        => _store.TryGetValue(txId, out value);

    IEnumerator<KeyValuePair<EvidenceId, EvidenceBase>> IEnumerable<KeyValuePair<EvidenceId, EvidenceBase>>.GetEnumerator()
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

    internal ImmutableSortedSet<EvidenceBase> Collect()
    {
        return [.. Values];
    }
}
