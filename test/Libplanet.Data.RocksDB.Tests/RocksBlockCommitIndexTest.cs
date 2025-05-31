using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksBlockCommitIndexTest(ITestOutputHelper output)
    : RocksKeyedIndexTestBase<BlockHash, BlockCommit, BlockCommitIndex>(output)
{
    protected override BlockCommitIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockCommit CreateValue(Random random) => RandomUtility.BlockCommit(random);
}
