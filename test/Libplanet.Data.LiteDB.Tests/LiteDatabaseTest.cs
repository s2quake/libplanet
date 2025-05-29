using System.Collections.Concurrent;
using System.IO;
using Libplanet.Data.Tests;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteDatabaseTest : DatabaseTestBase<LiteDatabase>, IDisposable
{
    private readonly ConcurrentBag<LiteDatabase> _databases = [];

    public override LiteDatabase CreateDatabase(string name)
    {
        var path = name != string.Empty
            ? Path.Combine(Path.GetTempPath(), $"{nameof(LiteDatabaseTest)}_{name}") : string.Empty;
        var database = new LiteDatabase(path);
        _databases.Add(database);
        return database;
    }

    public void Dispose()
    {
        foreach (var database in _databases)
        {
            database.Dispose();
            if (Directory.Exists(database.Path))
            {
                Directory.Delete(database.Path, true);
            }
        }
    }
}
