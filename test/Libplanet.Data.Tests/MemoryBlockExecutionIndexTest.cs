using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.Tests;

public sealed class MemoryBlockExecutionIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<BlockHash, BlockExecutionInfo, BlockExecutionIndex>(output)
{
    protected override BlockExecutionIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => Rand.BlockHash(random);

    protected override BlockExecutionInfo CreateValue(Random random) => Rand.BlockExecution(random);
}
