using System.Collections.Concurrent;
using System.IO;
using Libplanet.Data.Tests;
using LiteDB;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteTableTest : TableTestBase, IDisposable
{
    private readonly global::LiteDB.LiteDatabase _db = CreateLiteDatabase();
    private readonly ConcurrentBag<LiteTable> _tables = [];

    public override ITable CreateTable(string key)
    {
        var table = new LiteTable(_db, key);
        _tables.Add(table);
        return table;
    }

    public void Dispose()
    {
        foreach (var table in _tables)
        {
            table.Dispose();
        }

        _db.Dispose();
    }

    private static global::LiteDB.LiteDatabase CreateLiteDatabase()
    {
        var directoryName = Path.Combine(Path.GetTempPath(), $"{nameof(LiteTableTest)}_{Guid.NewGuid()}");
        var connectionString = new ConnectionString
        {
            Filename = Path.Combine(directoryName, "index.ldb"),
        };

        if (!Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        return new global::LiteDB.LiteDatabase(connectionString);
    }
}
