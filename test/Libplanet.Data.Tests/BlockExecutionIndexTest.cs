using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class BlockExecutionIndexTest(ITestOutputHelper output)
    : KeyedIndexTestBase<BlockHash, BlockExecution>(output)
{
    protected override KeyedIndexBase<BlockHash, BlockExecution> CreateIndex(bool useCache)
        => new BlockExecutionIndex(new MemoryDatabase(), useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockExecution CreateValue(Random random) => RandomUtility.BlockExecution(random);
}
