using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksNonceIndexTest(ITestOutputHelper output)
    : RocksIndexTestBase<Address, long, NonceIndex>(output)
{
    protected override NonceIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override Address CreateKey(Random random) => Rand.Address(random);

    protected override long CreateValue(Random random) => Rand.Int64(random);
}
