using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Data;
using Libplanet.Types.Evidence;

namespace Libplanet.Blockchain;

public sealed class EvidenceCollection(Repository repository)
    : IReadOnlyDictionary<EvidenceId, EvidenceBase>
{
    private readonly CommittedEvidenceStore _store = repository.CommittedEvidences;

    public IEnumerable<EvidenceId> Keys => _store.Keys;

    public IEnumerable<EvidenceBase> Values => _store.Values;

    public int Count => _store.Count;

    public EvidenceBase this[EvidenceId evidenceId] => _store[evidenceId];

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
}
