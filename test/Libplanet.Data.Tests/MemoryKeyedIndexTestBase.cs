using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public abstract class MemoryKeyedIndexTestBase<TKey, TValue, TIndex>(ITestOutputHelper output)
    : KeyedIndexTestBase<TKey, TValue, TIndex, MemoryDatabase>(output)
    where TKey : notnull
    where TValue : IHasKey<TKey>
    where TIndex : KeyedIndexBase<TKey, TValue>
{
    protected override MemoryDatabase CreateDatabase(string name) => new();

    protected override void DeleteDatabase(MemoryDatabase database)
    {
    }
}
