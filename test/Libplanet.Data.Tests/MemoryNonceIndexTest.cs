using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.Tests;

public sealed class MemoryNonceIndexTest(ITestOutputHelper output)
    : MemoryIndexTestBase<Address, long, NonceIndex>(output)
{
    protected override NonceIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override Address CreateKey(Random random) => Rand.Address(random);

    protected override long CreateValue(Random random) => Rand.Int64(random);
}
