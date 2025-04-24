using Libplanet.Store.Trie;
using RocksDbSharp;

namespace Libplanet.RocksDBStore;

public sealed class RocksDBKeyValueStore : IKeyValueStore
{
    private readonly RocksDb _keyValueDb;
    private bool _disposed;

    public RocksDBKeyValueStore(
        string path,
        RocksDBInstanceType type = RocksDBInstanceType.Primary,
        DbOptions? options = null)
    {
        options ??= new DbOptions()
            .SetCreateIfMissing()
            .SetSoftPendingCompactionBytesLimit(1000000000000)
            .SetHardPendingCompactionBytesLimit(1038176821042);

        _keyValueDb = RocksDBUtils.OpenRocksDb(options, path, type: type);
        Type = type;
    }

    public RocksDBInstanceType Type { get; }

    public IEnumerable<KeyBytes> Keys
    {
        get
        {
            using Iterator it = _keyValueDb.NewIterator();
            for (it.SeekToFirst(); it.Valid(); it.Next())
            {
                yield return KeyBytes.Create(it.Key());
            }
        }
    }

    public byte[] this[in KeyBytes key]
    {
        get => _keyValueDb.Get(key.AsSpan())
            ?? throw new KeyNotFoundException($"No such key: ${key}.");
        set => _keyValueDb.Put(key.AsSpan(), value);
    }

    public void SetMany(IDictionary<KeyBytes, byte[]> values)
    {
        using var writeBatch = new WriteBatch();

        foreach (var (key, value) in values)
        {
            writeBatch.Put(key.AsSpan(), value);
        }

        _keyValueDb.Write(writeBatch);
    }

    public bool Remove(in KeyBytes key)
    {
        _keyValueDb.Remove(key.AsSpan());
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _keyValueDb.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public bool ContainsKey(in KeyBytes key) => _keyValueDb.Get(key.AsSpan()) is { };

    public void TryCatchUpWithPrimary()
    {
        _keyValueDb.TryCatchUpWithPrimary();
    }
}
