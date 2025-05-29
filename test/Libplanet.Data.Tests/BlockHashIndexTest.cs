using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class BlockHashIndexTest(ITestOutputHelper output)
    : IndexTestBase<int, BlockHash>(output)
{
    protected override IndexBase<int, BlockHash> CreateIndex(bool useCache)
        => new BlockHashIndex(new MemoryDatabase(), useCache ? 100 : 0);

    protected override int CreateKey(Random random) => RandomUtility.Int32(random);

    protected override BlockHash CreateValue(Random random) => RandomUtility.BlockHash(random);
}
