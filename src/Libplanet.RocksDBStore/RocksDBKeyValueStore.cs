using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using Libplanet.Data;
using Libplanet.Data.Structures;
using RocksDbSharp;

namespace Libplanet.RocksDBStore;

public sealed class RocksDBKeyValueStore : TableBase, IDisposable
{
    private readonly string _path;
    private readonly RocksDBInstanceType _type;
    private readonly DbOptions _options;
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
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

    public override byte[] this[string key]
    {
        get => _rocksDb.Get(Encoding.UTF8.GetBytes(key)) ?? throw new KeyNotFoundException($"No such key: ${key}.");
        set
        {
            var exists = _rocksDb.Get(Encoding.UTF8.GetBytes(key)) is { };
            _rocksDb.Put(Encoding.UTF8.GetBytes(key), value);
            if (!exists && _count is not null)
            {
                _count++;
            }
        }
    }

    public override bool Remove(string key)
    {
        if (_rocksDb.Get(Encoding.UTF8.GetBytes(key)) is { })
        {
            _rocksDb.Remove(Encoding.UTF8.GetBytes(key));
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
            Trace.WriteLine($"Disposing RocksDB instance at {_path}");
            GC.SuppressFinalize(this);
        }
    }

    public override bool ContainsKey(string key) => _rocksDb.Get(Encoding.UTF8.GetBytes(key)) is { };

    public void TryCatchUpWithPrimary()
    {
        _rocksDb.TryCatchUpWithPrimary();
    }

    public override void Add(string key, byte[] value)
    {
        if (_rocksDb.Get(Encoding.UTF8.GetBytes(key)) is { })
        {
            throw new ArgumentException($"Key {key} already exists", nameof(key));
        }

        _rocksDb.Put(Encoding.UTF8.GetBytes(key), value);
        if (_count is not null)
        {
            _count++;
        }
    }

    public override bool TryGetValue(string key, [MaybeNullWhen(false)] out byte[] value)
    {
        if (_rocksDb.Get(Encoding.UTF8.GetBytes(key)) is { } bytes)
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
        Trace.WriteLine($"Clearing RocksDB instance at {_path}");
        if (Directory.Exists(_path))
        {
            Directory.Delete(_path, recursive: true);
        }

        _rocksDb = CreateInstance();
        _count = null;
    }

    protected override IEnumerable<string> EnumerateKeys()
    {
        using var it = _rocksDb.NewIterator();
        for (it.SeekToFirst(); it.Valid(); it.Next())
        {
            yield return Encoding.UTF8.GetString(it.Key());
        }
    }

    private static DbOptions CreateDbOptions(DbOptions? options)
    {
        return new DbOptions()
            .SetCreateIfMissing()
            .SetCreateMissingColumnFamilies()
            .SetAllowConcurrentMemtableWrite(true)
            .SetMaxFileOpeningThreads(5)
            .SetSoftPendingCompactionBytesLimit(1000000000000)
            .SetHardPendingCompactionBytesLimit(1038176821042);
    }

    private RocksDb CreateInstance()
    {
        Trace.WriteLine($"Creating RocksDB instance at {_path}");
        return RocksDBUtils.OpenRocksDb(_options, _path, type: _type);
    }
}
