using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteBlockCommitIndexTest(ITestOutputHelper output)
    : LiteKeyedIndexTestBase<BlockHash, BlockCommit, BlockCommitIndex>(output)
{
    protected override BlockCommitIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => Rand.BlockHash(random);

    protected override BlockCommit CreateValue(Random random) => Rand.BlockCommit(random);
}
