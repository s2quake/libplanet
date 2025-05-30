using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public class BlockDigestIndexTest(ITestOutputHelper output)
    : KeyedIndexTestBase<BlockHash, BlockDigest, BlockDigestIndex>(output)
{
    protected override BlockDigestIndex CreateIndex(string name, bool useCache)
        => new(new MemoryDatabase(), useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override BlockDigest CreateValue(Random random) => RandomUtility.BlockDigest(random);
}
