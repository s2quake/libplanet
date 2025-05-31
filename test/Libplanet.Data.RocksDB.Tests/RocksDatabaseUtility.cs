using System.IO;

namespace Libplanet.Data.RocksDB.Tests;

public static class RocksDatabaseUtility
{
    public static RocksDatabase CreateDatabase(object obj, string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{obj.GetType().Name}_{name}_{Guid.NewGuid()}");
        var database = new RocksDatabase(path);
        return database;
    }
}
