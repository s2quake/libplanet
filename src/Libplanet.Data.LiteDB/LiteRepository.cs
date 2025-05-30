namespace Libplanet.Data.LiteDB;

public sealed class LiteRepository(string path)
    : Repository<LiteDatabase>(new LiteDatabase(path)), IDisposable
{
    private bool _disposed;

    public LiteRepository()
        : this(string.Empty)
    {
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Database.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
