using System.Diagnostics.CodeAnalysis;
using Libplanet.Store.Trie;
using RocksDbSharp;

namespace Libplanet.RocksDBStore;

public sealed class RocksDBKeyValueStore : KeyValueStoreBase, IDisposable
{
    private readonly RocksDb _rocksDb;
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

        _rocksDb = RocksDBUtils.OpenRocksDb(options, path, type: type);
        Type = type;
    }

    public RocksDBInstanceType Type { get; }

    public override byte[] this[KeyBytes key]
    {
        get => _rocksDb.Get(key.AsSpan())
            ?? throw new KeyNotFoundException($"No such key: ${key}.");
        set => _rocksDb.Put(key.AsSpan(), value);
    }

    public void SetMany(IDictionary<KeyBytes, byte[]> values)
    {
        using var writeBatch = new WriteBatch();

        foreach (var (key, value) in values)
        {
            writeBatch.Put(key.AsSpan(), value);
        }

        _rocksDb.Write(writeBatch);
    }

    public override bool Remove(KeyBytes key)
    {
        _rocksDb.Remove(key.AsSpan());
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _rocksDb.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public override bool ContainsKey(KeyBytes key) => _rocksDb.Get(key.AsSpan()) is { };

    public void TryCatchUpWithPrimary()
    {
        _rocksDb.TryCatchUpWithPrimary();
    }

    public override void Add(KeyBytes key, byte[] value)
    {
        if (_rocksDb.Get(key.AsSpan()) is { })
        {
            throw new ArgumentException($"Key {key} already exists", nameof(key));
        }

        _rocksDb.Put(key.AsSpan(), value);
    }

    public override bool TryGetValue(KeyBytes key, [MaybeNullWhen(false)] out byte[] value)
    {
        if (_rocksDb.Get(key.AsSpan()) is { } bytes)
        {
            value = bytes;
            return true;
        }

        value = null;
        return false;
    }

    public override void Clear() => throw new NotSupportedException("Clear is not supported.");

    protected override IEnumerable<KeyBytes> EnumerateKeys()
    {
        using var it = _rocksDb.NewIterator();
        for (it.SeekToFirst(); it.Valid(); it.Next())
        {
            yield return new KeyBytes(it.Key());
        }
    }
}
