using System.IO;
using Libplanet.Data;
using RocksDbSharp;

namespace Libplanet.RocksDBStore;

public sealed class RocksDatabase : Database<RocksDBKeyValueStore>
{
    private readonly DbOptions _options;
    private readonly ColumnFamilyOptions _colOptions;
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
        _colOptions = new ColumnFamilyOptions();

        if (maxTotalWalSize is ulong maxTotalWalSizeValue)
        {
            _options = _options.SetMaxTotalWalSize(maxTotalWalSizeValue);
            _colOptions = _colOptions.SetMaxTotalWalSize(maxTotalWalSizeValue);
        }

        if (keepLogFileNum is ulong keepLogFileNumValue)
        {
            _options = _options.SetKeepLogFileNum(keepLogFileNumValue);
            _colOptions = _colOptions.SetKeepLogFileNum(keepLogFileNumValue);
        }

        if (maxLogFileSize is ulong maxLogFileSizeValue)
        {
            _options = _options.SetMaxLogFileSize(maxLogFileSizeValue);
            _colOptions = _colOptions.SetMaxLogFileSize(maxLogFileSizeValue);
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

    protected override RocksDBKeyValueStore Create(string key)
    {
        return new RocksDBKeyValueStore(Path.Combine(_path, key), _type, _options);
    }

    protected override void OnRemove(string key, RocksDBKeyValueStore value)
    {
        value.Dispose();
        if (Directory.Exists(value.Path))
        {
            Directory.Delete(value.Path, recursive: true);
        }
    }
}
