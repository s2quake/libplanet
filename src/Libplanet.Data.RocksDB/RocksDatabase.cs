using System.IO;
using RocksDbSharp;

namespace Libplanet.Data.RocksDB;

public sealed class RocksDatabase : Database<RocksTable>
{
    private readonly DbOptions _options;
    private readonly string _path;
    private readonly RocksDBInstanceType _type;

    public RocksDatabase(
        string path,
        ulong? maxTotalWalSize = null,
        ulong? keepLogFileNum = null,
        ulong? maxLogFileSize = null,
        RocksDBInstanceType type = RocksDBInstanceType.Primary)
    {
        _path = path;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var value in Values)
            {
                value.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    protected override RocksTable Create(string key)
    {
        return new RocksTable(Path.Combine(_path, key), _type, _options);
    }

    protected override void OnRemove(string key, RocksTable value)
    {
        value.Dispose();
        if (Directory.Exists(value.Path))
        {
            Directory.Delete(value.Path, recursive: true);
        }
    }
}
