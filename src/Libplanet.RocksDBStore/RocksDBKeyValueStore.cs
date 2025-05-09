using System.Diagnostics.CodeAnalysis;
using System.IO;
using Libplanet.Store;
using Libplanet.Store.Trie;
using RocksDbSharp;

namespace Libplanet.RocksDBStore;

public sealed class RocksDBKeyValueStore : KeyValueStoreBase, IDisposable
{
    private readonly string _path;
    private readonly RocksDBInstanceType _type;
    private readonly DbOptions _options;
    private RocksDb _rocksDb;
    private bool _disposed;
    private int? _count;

    public RocksDBKeyValueStore(
        string path, RocksDBInstanceType type = RocksDBInstanceType.Primary, DbOptions? options = null)
    {
        _path = path;
        _type = type;
        _options = CreateDbOptions(options);
        _rocksDb = CreateInstance();
    }

    public string Path => _path;

    public override int Count
    {
        get
        {
            if (_count is null)
            {
                var count = 0;
                using var it = _rocksDb.NewIterator();
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    count++;
                }

                _count = count;
                return count;
            }

            return _count.Value;
        }
    }

    public override byte[] this[KeyBytes key]
    {
        get => _rocksDb.Get(key.AsSpan()) ?? throw new KeyNotFoundException($"No such key: ${key}.");
        set
        {
            var exists = _rocksDb.Get(key.AsSpan()) is { };
            _rocksDb.Put(key.AsSpan(), value);
            if (!exists && _count is not null)
            {
                _count++;
            }
        }
    }

    public override bool Remove(KeyBytes key)
    {
        if (_rocksDb.Get(key.AsSpan()) is { })
        {
            _rocksDb.Remove(key.AsSpan());
            if (_count is not null)
            {
                _count--;
            }

            return true;
        }

        return false;
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
        if (_count is not null)
        {
            _count++;
        }
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

    public override void Clear()
    {
        _rocksDb.Dispose();
        if (Directory.Exists(_path))
        {
            Directory.Delete(_path, recursive: true);
        }

        _rocksDb = CreateInstance();
        _count = null;
    }

    protected override IEnumerable<KeyBytes> EnumerateKeys()
    {
        using var it = _rocksDb.NewIterator();
        for (it.SeekToFirst(); it.Valid(); it.Next())
        {
            yield return new KeyBytes(it.Key());
        }
    }

    private static DbOptions CreateDbOptions(DbOptions? options)
    {
        return options ?? new DbOptions()
            .SetCreateIfMissing()
            .SetSoftPendingCompactionBytesLimit(1000000000000)
            .SetHardPendingCompactionBytesLimit(1038176821042);
    }

    private RocksDb CreateInstance() => RocksDBUtils.OpenRocksDb(_options, _path, type: _type);
}
