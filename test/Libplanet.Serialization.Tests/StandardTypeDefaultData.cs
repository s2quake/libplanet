using System.Collections;

namespace Libplanet.Serialization.Tests;

public sealed class StandardTypeDefaultData : IEnumerable<TheoryDataRow<object>>
{
    public IEnumerator<TheoryDataRow<object>> GetEnumerator()
    {
        yield return new TheoryDataRow<object>(default(BigInteger));
        yield return new TheoryDataRow<object>(default(bool));
        yield return new TheoryDataRow<object>(default(byte));
        yield return new TheoryDataRow<object>(default(char));
        yield return new TheoryDataRow<object>(default(DateTimeOffset));
        yield return new TheoryDataRow<object>(default(Guid));
        yield return new TheoryDataRow<object>(default(int));
        yield return new TheoryDataRow<object>(default(long));
        yield return new TheoryDataRow<object>(default(TimeSpan));
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
