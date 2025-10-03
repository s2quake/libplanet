using System.Collections;

namespace Libplanet.Serialization.Tests;

public sealed class StandardTypeData : IEnumerable<TheoryDataRow<object>>
{
    public IEnumerator<TheoryDataRow<object>> GetEnumerator()
    {
        yield return new TheoryDataRow<object>((BigInteger)0);
        yield return new TheoryDataRow<object>((BigInteger)1);
        yield return new TheoryDataRow<object>(true);
        yield return new TheoryDataRow<object>(false);
        yield return new TheoryDataRow<object>(Array.Empty<byte>());
        yield return new TheoryDataRow<object>(new byte[] { 0, 1, 2, 3 });
        yield return new TheoryDataRow<object>(DateTimeOffset.MinValue);
        yield return new TheoryDataRow<object>(DateTimeOffset.MaxValue);
        yield return new TheoryDataRow<object>(ImmutableArray<byte>.Empty);
        yield return new TheoryDataRow<object>(ImmutableArray.Create<byte>(0, 1, 2, 3));
        yield return new TheoryDataRow<object>(0);
        yield return new TheoryDataRow<object>(1);
        yield return new TheoryDataRow<object>(0L);
        yield return new TheoryDataRow<object>(1L);
        yield return new TheoryDataRow<object>(string.Empty);
        yield return new TheoryDataRow<object>("Hello, World!");
        yield return new TheoryDataRow<object>(TimeSpan.Zero);
        yield return new TheoryDataRow<object>(TimeSpan.FromSeconds(1));
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
