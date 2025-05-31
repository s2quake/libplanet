using Libplanet.Data.Tests;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Data.LiteDB.Tests;

public abstract class LiteKeyedIndexTestBase<TKey, TValue, TIndex>(ITestOutputHelper output)
    : KeyedIndexTestBase<TKey, TValue, TIndex, LiteDatabase>(output)
    where TKey : notnull
    where TValue : IHasKey<TKey>
    where TIndex : KeyedIndexBase<TKey, TValue>
{
    protected override LiteDatabase CreateDatabase(string name)
        => LiteDatabaseUtility.CreateDatabase(this, name);
}
