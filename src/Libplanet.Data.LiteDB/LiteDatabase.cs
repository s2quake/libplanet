using System.IO;

namespace Libplanet.Data.LiteDB;

public sealed class LiteDatabase(string path) : Database<LiteTable>, IDisposable
{
    private readonly global::LiteDB.LiteDatabase _db = CreateLiteDatabase(path);
    private bool _disposed;

    public string Path { get; } = path;

    protected override LiteTable Create(string key) => new(_db, key);

    protected override void OnRemove(string key, LiteTable value)
    {
        base.OnRemove(key, value);
        if (!_disposed)
        {
            value.Dispose();
        }
    }

    private static global::LiteDB.LiteDatabase CreateLiteDatabase(string path)
    {
        if (path == string.Empty)
        {
            return new global::LiteDB.LiteDatabase(new MemoryStream());
        }

        if (!System.IO.Path.IsPathRooted(path))
        {
            throw new ArgumentException("The path must be an absolute path.", nameof(path));
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return new global::LiteDB.LiteDatabase(System.IO.Path.Combine(path, "database.db"));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _db.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
