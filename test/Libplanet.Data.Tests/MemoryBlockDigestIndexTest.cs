using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.Tests;

public sealed class MemoryBlockDigestIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<BlockHash, BlockDigest, BlockDigestIndex>(output)
{
    protected override BlockDigestIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => Rand.BlockHash(random);

    protected override BlockDigest CreateValue(Random random) => Rand.BlockDigest(random);
}
