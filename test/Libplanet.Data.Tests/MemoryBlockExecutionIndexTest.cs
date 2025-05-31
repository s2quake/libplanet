using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryBlockExecutionIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<BlockHash, BlockExecution, BlockExecutionIndex>(output)
{
    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockExecution CreateValue(Random random) => RandomUtility.BlockExecution(random);
}
