namespace Libplanet.Data.RocksDB;

public sealed class RocksRepository(string path) : Repository<RocksDatabase>(new RocksDatabase(path)), IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            Database.Dispose();
            GC.SuppressFinalize(this);
            _disposed = true;
        }
    }
}
