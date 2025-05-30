using System.IO;

namespace Libplanet.Data.LiteDB.Tests;

public static class LiteDatabaseUtility
{
    public static LiteDatabase CreateDatabase(object obj, string name)
    {
        var path = name != string.Empty
            ? Path.Combine(Path.GetTempPath(), $"{obj.GetType().Name}_{name}") : string.Empty;
        var database = new LiteDatabase(path);
        return database;
    }
}
