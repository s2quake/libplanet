using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryBlockCommitIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<BlockHash, BlockCommit, BlockCommitIndex>(output)
{
    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockCommit CreateValue(Random random) => RandomUtility.BlockCommit(random);
}
