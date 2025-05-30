using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class BlockDigestIndexTest(ITestOutputHelper output)
    : KeyedIndexTestBase<BlockHash, BlockDigest>(output)
{
    protected override KeyedIndexBase<BlockHash, BlockDigest> CreateIndex(bool useCache)
        => new BlockDigestIndex(new MemoryDatabase(), useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockDigest CreateValue(Random random) => RandomUtility.BlockDigest(random);
}
