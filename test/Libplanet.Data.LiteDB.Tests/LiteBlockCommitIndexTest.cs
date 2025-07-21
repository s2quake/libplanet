using Libplanet.TestUtilities;
using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteBlockCommitIndexTest(ITestOutputHelper output)
    : LiteIndexTestBase<BlockHash, BlockCommit, BlockCommitIndex>(output)
{
    protected override BlockCommitIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockCommit CreateValue(Random random) => RandomUtility.BlockCommit(random);
}
