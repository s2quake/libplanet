using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.Tests;

public sealed class MemoryBlockCommitIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<BlockHash, BlockCommit, BlockCommitIndex>(output)
{
    protected override BlockCommitIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => Rand.BlockHash(random);

    protected override BlockCommit CreateValue(Random random) => Rand.BlockCommit(random);
}
