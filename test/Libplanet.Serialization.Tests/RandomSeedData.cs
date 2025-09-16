using System.Collections;

namespace Libplanet.Serialization.Tests;

public sealed class RandomSeedData : IEnumerable<TheoryDataRow<int>>
{
    public IEnumerator<TheoryDataRow<int>> GetEnumerator()
    {
        yield return new TheoryDataRow<int>(Random.Shared.Next());
        yield return new TheoryDataRow<int>(Random.Shared.Next());
        yield return new TheoryDataRow<int>(Random.Shared.Next());
        yield return new TheoryDataRow<int>(Random.Shared.Next());
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
