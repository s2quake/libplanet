using System.IO;

namespace Libplanet.Data.LiteDB;

public sealed class LiteDatabase(string path) : Database<LiteTable>
{
    private readonly global::LiteDB.LiteDatabase _db = CreateLiteDatabase(path);

    protected override LiteTable Create(string key)
    {
        return new LiteTable(_db, key);
    }

    private static global::LiteDB.LiteDatabase CreateLiteDatabase(string path)
    {
        if (path == string.Empty)
        {
            return new global::LiteDB.LiteDatabase(new MemoryStream());
        }

        if (!Path.IsPathRooted(path))
        {
            throw new ArgumentException("The path must be an absolute path.", nameof(path));
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return new global::LiteDB.LiteDatabase(Path.Combine(path, "database.db"));
    }
}
