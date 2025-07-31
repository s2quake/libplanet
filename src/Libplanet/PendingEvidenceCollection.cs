using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Subjects;
using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet;

public sealed class PendingEvidenceCollection(Repository repository)
    : IReadOnlyDictionary<EvidenceId, EvidenceBase>
{
    private readonly Subject<EvidenceBase> _addedSubject = new();
    private readonly Subject<EvidenceBase> _removedSubject = new();
    private readonly PendingEvidenceIndex _store = repository.PendingEvidences;

    public IObservable<EvidenceBase> Added => _addedSubject;

    public IObservable<EvidenceBase> Removed => _removedSubject;

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

        _addedSubject.OnNext(evidence);
    }

    public bool Remove(EvidenceId evidenceId)
    {
        if (_store.TryGetValue(evidenceId, out var evidence))
        {
            _removedSubject.OnNext(evidence);
            return true;
        }

        return false;
    }

    public bool ContainsKey(EvidenceId evidenceId) => _store.ContainsKey(evidenceId);

    public bool TryGetValue(EvidenceId evidenceId, [MaybeNullWhen(false)] out EvidenceBase value)
        => _store.TryGetValue(evidenceId, out value);

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
