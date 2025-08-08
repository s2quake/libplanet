using System.Collections;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

internal sealed class RoundCollection : IEnumerable<Round>
{
    private readonly int _height;
    private readonly ImmutableSortedSet<Validator> _validators;
    private readonly SortedDictionary<int, Round> _roundByIndex = [];

    public RoundCollection(int height, ImmutableSortedSet<Validator> validators)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        if (validators.Count is 0)
        {
            throw new ArgumentException("Validators cannot be empty.", nameof(validators));
        }

        _height = height;
        _validators = validators;
    }

    public Round this[int index]
    {
        get
        {
            if (!_roundByIndex.TryGetValue(index, out var consensusRound))
            {
                consensusRound = new Round(_height, index, _validators);
                _roundByIndex[index] = consensusRound;
            }

            return consensusRound;
        }
    }

    public void Clear() => _roundByIndex.Clear();

    public int Count => _roundByIndex.Count;

    public IEnumerator<Round> GetEnumerator()
    {
        foreach (var round in _roundByIndex.Values)
        {
            yield return round;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
