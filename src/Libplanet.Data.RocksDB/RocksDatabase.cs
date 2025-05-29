using System.IO;
using RocksDbSharp;

namespace Libplanet.Data.RocksDB;

public sealed class RocksDatabase : Database<RocksTable>, IDisposable
{
    private readonly DbOptions _options;
    private readonly RocksDBInstanceType _type;
    private bool _disposed;

    public RocksDatabase(
        string path,
        ulong? maxTotalWalSize = null,
        ulong? keepLogFileNum = null,
        ulong? maxLogFileSize = null,
        RocksDBInstanceType type = RocksDBInstanceType.Primary)
    {
        Path = path;
        _options = new DbOptions()
            .SetCreateIfMissing();

        if (maxTotalWalSize is ulong maxTotalWalSizeValue)
        {
            _options = _options.SetMaxTotalWalSize(maxTotalWalSizeValue);
        }

        if (keepLogFileNum is ulong keepLogFileNumValue)
        {
            _options = _options.SetKeepLogFileNum(keepLogFileNumValue);
        }

        if (maxLogFileSize is ulong maxLogFileSizeValue)
        {
            _options = _options.SetMaxLogFileSize(maxLogFileSizeValue);
        }

        _type = type;
    }

    public string Path { get; }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var value in Values)
            {
                value.Dispose();
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    protected override RocksTable Create(string key) => new(System.IO.Path.Combine(Path, key), _type, _options);

    protected override void OnRemove(string key, RocksTable value)
    {
        if (!_disposed)
        {
            value.Dispose();
            if (Directory.Exists(value.Path))
            {
                Directory.Delete(value.Path, recursive: true);
            }
        }
    }
}
