using System.Collections.Concurrent;
using System.IO;
using Libplanet.Data.Tests;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksDatabaseTest : DatabaseTestBase<RocksDatabase>, IDisposable
{
    private readonly ConcurrentBag<RocksDatabase> _databases = [];

    public override RocksDatabase CreateDatabase(string name)
    {
        var path = name != string.Empty
            ? Path.Combine(Path.GetTempPath(), $"{nameof(RocksDatabaseTest)}_{name}") : string.Empty;
        var database = new RocksDatabase(path);
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
