using System.IO;
using Libplanet.Data.Tests;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Data.RocksDB.Tests;

public abstract class RocksKeyedIndexTestBase<TKey, TValue, TIndex>(ITestOutputHelper output)
    : KeyedIndexTestBase<TKey, TValue, TIndex, RocksDatabase>(output)
    where TKey : notnull
    where TValue : IHasKey<TKey>
    where TIndex : KeyedIndexBase<TKey, TValue>
{
    protected override RocksDatabase CreateDatabase(string name) => RocksDatabaseUtility.CreateDatabase(this, name);

    protected override void DeleteDatabase(RocksDatabase database)
    {
        database.Dispose();
        if (Directory.Exists(database.Path))
        {
            Directory.Delete(database.Path, true);
        }
    }
}
