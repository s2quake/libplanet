using Libplanet.TestUtilities;
using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryBlockExecutionIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<BlockHash, BlockExecution, BlockExecutionIndex>(output)
{
    protected override BlockExecutionIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockExecution CreateValue(Random random) => RandomUtility.BlockExecution(random);
}
