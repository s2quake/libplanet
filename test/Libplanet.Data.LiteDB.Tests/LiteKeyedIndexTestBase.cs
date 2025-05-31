using System.IO;
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
    protected override LiteDatabase CreateDatabase(string name) => LiteDatabaseUtility.CreateDatabase(this, name);

    protected override void DeleteDatabase(LiteDatabase database)
    {
        database.Dispose();
        if (Directory.Exists(database.Path))
        {
            Directory.Delete(database.Path, true);
        }
    }
}
