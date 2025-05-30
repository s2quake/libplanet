using System.Collections.Concurrent;
using System.IO;
using Libplanet.Data.Tests;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksTableTest : TableTestBase<RocksTable>, IDisposable
{
    private readonly string _directoryName
        = Path.Combine(Path.GetTempPath(), $"{nameof(RocksTableTest)}_{Guid.NewGuid()}");
    private readonly ConcurrentBag<RocksTable> _tables = [];

    public override RocksTable CreateTable(string name)
    {
        if (!Directory.Exists(_directoryName))
        {
            Directory.CreateDirectory(_directoryName);
        }

        var path = Path.Combine(_directoryName, name);
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
