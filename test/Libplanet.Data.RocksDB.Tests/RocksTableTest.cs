using System.Collections.Concurrent;
using System.IO;
using Libplanet.Data.Tests;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksTableTest : TableTestBase, IDisposable
{
    private readonly string _directoryName
        = Path.Combine(Path.GetTempPath(), $"{nameof(RocksTableTest)}_{Guid.NewGuid()}");
    private readonly ConcurrentBag<RocksTable> _tables = [];

    public override ITable CreateTable(string key)
    {
        if (!Directory.Exists(_directoryName))
        {
            Directory.CreateDirectory(_directoryName);
        }

        var path = Path.Combine(_directoryName, key);
        var table = new RocksTable(path);
        _tables.Add(table);
        return table;
    }

    public void Dispose()
    {
        foreach (var table in _tables)
        {
            table.Dispose();
        }
    }
}
