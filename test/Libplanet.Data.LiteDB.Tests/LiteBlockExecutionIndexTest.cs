using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteBlockExecutionIndexTest(ITestOutputHelper output)
    : LiteKeyedIndexTestBase<BlockHash, BlockExecutionInfo, BlockExecutionIndex>(output)
{
    protected override BlockExecutionIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockExecutionInfo CreateValue(Random random) => RandomUtility.BlockExecution(random);
}
