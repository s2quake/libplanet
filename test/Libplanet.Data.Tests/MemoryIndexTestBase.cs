using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public abstract class MemoryIndexTestBase<TKey, TValue, TIndex>(ITestOutputHelper output)
    : IndexTestBase<TKey, TValue, TIndex, MemoryDatabase>(output)
    where TKey : notnull
    where TValue : notnull
    where TIndex : IndexBase<TKey, TValue>
{
    protected override MemoryDatabase CreateDatabase(string name) => new();

    protected override void DeleteDatabase(MemoryDatabase database)
    {
    }
}
