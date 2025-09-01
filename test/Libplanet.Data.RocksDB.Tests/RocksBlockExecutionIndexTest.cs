using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksBlockExecutionIndexTest(ITestOutputHelper output)
    : RocksKeyedIndexTestBase<BlockHash, BlockExecutionInfo, BlockExecutionIndex>(output)
{
    protected override BlockExecutionIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockExecutionInfo CreateValue(Random random) => RandomUtility.BlockExecution(random);
}
