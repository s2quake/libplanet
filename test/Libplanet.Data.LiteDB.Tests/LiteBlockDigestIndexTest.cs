using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteBlockDigestIndexTest(ITestOutputHelper output)
    : LiteKeyedIndexTestBase<BlockHash, BlockDigest, BlockDigestIndex>(output)
{
    protected override BlockDigestIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockDigest CreateValue(Random random) => RandomUtility.BlockDigest(random);
}
