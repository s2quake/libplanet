using System.IO;
using Libplanet.Data.Tests;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksRepositoryTest(ITestOutputHelper output) : RepositoryTestBase<RocksRepository>(output)
{
    protected override RocksRepository CreateRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{nameof(RocksRepositoryTest)}_{Guid.NewGuid()}");
        return new RocksRepository(path);
    }
}
