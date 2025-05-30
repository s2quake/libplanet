using Libplanet.Data.Tests;
using Xunit.Abstractions;
using static Libplanet.Data.RocksDB.Tests.RocksDatabaseUtility;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksStateRootHashIndexTest(ITestOutputHelper output) : StateRootHashIndexTest(output)
{
    protected override StateRootHashIndex CreateIndex(string name, bool useCache)
        => new(CreateDatabase(this, name), useCache ? 100 : 0);
}
