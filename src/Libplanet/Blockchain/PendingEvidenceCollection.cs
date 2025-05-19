using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Store;
using Libplanet.Types.Evidence;

namespace Libplanet.Blockchain;

public sealed class PendingEvidenceCollection(Repository repository, TimeSpan lifetime)
    : IReadOnlyDictionary<EvidenceId, EvidenceBase>
{
    private readonly PendingEvidenceStore _store = repository.PendingEvidences;

    public PendingEvidenceCollection(Repository repository)
        : this(repository, TimeSpan.FromSeconds(10 * 60))
    {
    }

    public TimeSpan Lifetime => lifetime;

    public IEnumerable<EvidenceId> Keys => _store.Keys;

    public IEnumerable<EvidenceBase> Values => _store.Values;

    public int Count => _store.Count;

    public EvidenceBase this[EvidenceId txId] => _store[txId];

    public bool Add(EvidenceBase evidence)
    {
        if (evidence.Timestamp + lifetime < DateTimeOffset.UtcNow)
        {
            return false;
        }

        // compare with repository genesis

        return _store.TryAdd(evidence);
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
}
