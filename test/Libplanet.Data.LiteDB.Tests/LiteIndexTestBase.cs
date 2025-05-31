using System.IO;
using Libplanet.Data.Tests;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Data.LiteDB.Tests;

public abstract class LiteIndexTestBase<TKey, TValue, TIndex>(ITestOutputHelper output)
    : IndexTestBase<TKey, TValue, TIndex, LiteDatabase>(output)
    where TKey : notnull
    where TValue : notnull
    where TIndex : IndexBase<TKey, TValue>
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
