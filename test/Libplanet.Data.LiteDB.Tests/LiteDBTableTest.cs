using System.IO;
using Libplanet.Data.Tests;
using LiteDB;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteDBTableTest : TableTestBase
{
    private readonly global::LiteDB.LiteDatabase _db = CreateLiteDatabase();

    public override ITable CreateTable(string key)
    {
        return new LiteTable(_db, key);
    }

    private static global::LiteDB.LiteDatabase CreateLiteDatabase()
    {
        var directoryName = Path.Combine(Path.GetTempPath(), $"{nameof(LiteDBTableTest)}_{Guid.NewGuid()}");
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
