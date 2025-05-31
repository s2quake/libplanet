using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public abstract class MemoryIndexTestBase<TKey, TValue, TIndex>(ITestOutputHelper output)
    : IndexTestBase<TKey, TValue, TIndex, MemoryDatabase>(output)
    where TKey : notnull
    where TValue : notnull
    where TIndex : IndexBase<TKey, TValue>
{
    protected override MemoryDatabase CreateDatabase(string name) => new();

    protected override TIndex CreateIndex(MemoryDatabase database, bool useCache)
        => (TIndex)Activator.CreateInstance(typeof(TIndex), database, useCache ? 100 : 0)!;
}
