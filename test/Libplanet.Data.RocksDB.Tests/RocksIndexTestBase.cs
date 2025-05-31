using Libplanet.Data.Tests;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Data.RocksDB.Tests;

public abstract class RocksIndexTestBase<TKey, TValue, TIndex>(ITestOutputHelper output)
    : IndexTestBase<TKey, TValue, TIndex, RocksDatabase>(output)
    where TKey : notnull
    where TValue : notnull
    where TIndex : IndexBase<TKey, TValue>
{
    protected override RocksDatabase CreateDatabase(string name)
        => RocksDatabaseUtility.CreateDatabase(this, name);
}
