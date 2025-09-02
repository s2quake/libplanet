using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Subjects;
using Libplanet.Data;
using Libplanet.Serialization;
using Libplanet.State;
using Libplanet.Types;
using Microsoft.CodeAnalysis;

namespace Libplanet;

public sealed class PendingEvidenceCollection(Repository repository, BlockchainOptions options)
    : IReadOnlyDictionary<EvidenceId, EvidenceBase>
{
    private readonly Subject<EvidenceBase> _addedSubject = new();
    private readonly Subject<EvidenceBase> _removedSubject = new();
    private readonly PendingEvidenceIndex _pendingIndex = repository.PendingEvidences;
    private readonly CommittedEvidenceIndex _committedIndex = repository.CommittedEvidences;

    public IObservable<EvidenceBase> Added => _addedSubject;

    public IObservable<EvidenceBase> Removed => _removedSubject;

    public IEnumerable<EvidenceId> Keys => _pendingIndex.Keys;

    public IEnumerable<EvidenceBase> Values => _pendingIndex.Values;

    public int Count => _pendingIndex.Count;

    public EvidenceBase this[EvidenceId txId] => _pendingIndex[txId];

    public bool TryAdd(EvidenceBase evidence)
    {
        if (_pendingIndex.TryAdd(evidence))
        {
            _addedSubject.OnNext(evidence);
            return true;
        }

        return false;
    }

    public void Add(EvidenceBase evidence)
    {
        if (_committedIndex.ContainsKey(evidence.Id))
        {
            throw new ArgumentException(
                $"Evidence with ID {evidence.Id} already exists in the committed index.",
                nameof(evidence));
        }

        if (IsHeightExpired(evidence, repository.Height))
        {
            throw new ArgumentException(
                $"Evidence with ID {evidence.Id} is too old to be added.",
                nameof(evidence));
        }

        Validate(evidence);

        if (!_pendingIndex.TryAdd(evidence))
        {
            throw new ArgumentException(
                $"Evidence with ID {evidence.Id} already exists in the collection.",
                nameof(evidence));
        }

        _addedSubject.OnNext(evidence);
    }

    public bool Remove(EvidenceId evidenceId)
    {
        if (_pendingIndex.TryGetValue(evidenceId, out var evidence))
        {
            _pendingIndex.Remove(evidenceId);
            _removedSubject.OnNext(evidence);
            return true;
        }

        return false;
    }

    public bool ContainsKey(EvidenceId evidenceId) => _pendingIndex.ContainsKey(evidenceId);

    public bool TryGetValue(EvidenceId evidenceId, [MaybeNullWhen(false)] out EvidenceBase value)
        => _pendingIndex.TryGetValue(evidenceId, out value);

    public void Prune()
    {
        var height = repository.Height;
        var query = from evidence in Values
                    where IsHeightExpired(evidence, height)
                    select evidence;
        var items = query.ToArray();
        foreach (var item in items)
        {
            Remove(item.Id);
        }
    }

    IEnumerator<KeyValuePair<EvidenceId, EvidenceBase>> IEnumerable<KeyValuePair<EvidenceId, EvidenceBase>>.GetEnumerator()
    {
        foreach (var kvp in _pendingIndex)
        {
            yield return kvp;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var kvp in _pendingIndex)
        {
            yield return kvp;
        }
    }

    public ImmutableArray<EvidenceBase> Collect(int height)
    {
        var items1 = Values.Where(item => !IsHeightExpired(item, height));
        return [.. items1];
    }

    private bool IsHeightExpired(EvidenceBase evidence, int height)
        => options.EvidenceOptions.IsHeightExpired(evidence, height);

    private void Validate(EvidenceBase evidence)
    {
        if (evidence.Height < repository.GenesisHeight)
        {
            throw new ArgumentException(
                $"Evidence with ID {evidence.Id} is from a previous epoch.",
                nameof(evidence));
        }

        try
        {
            var blockHash = repository.BlockHashes[evidence.Height];
            var stateRootHash = repository.StateRootHashes[blockHash];
            var world = new World(repository.States, stateRootHash);
            var validators = world.GetValidators();
            var evidenceContext = new EvidenceContext(validators);

            ModelValidationUtility.Validate(evidence, new Dictionary<object, object?>
            {
                { typeof(EvidenceContext), evidenceContext },
            });
        }
        catch (Exception e)
        {
            throw new ArgumentException(
                $"Evidence with ID {evidence.Id} is invalid: {e.Message}",
                nameof(evidence),
                e);
        }
    }
}
