using System.Collections;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

internal sealed class RoundCollection(int height, ImmutableSortedSet<Validator> validators)
    : IEnumerable<Round>
{
    private readonly SortedDictionary<int, Round> _roundByIndex = [];

    public Round this[int index]
    {
        get
        {
            if (!_roundByIndex.TryGetValue(index, out var consensusRound))
            {
                consensusRound = new Round(height, index, validators);
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
