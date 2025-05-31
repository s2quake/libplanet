using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksNonceIndexTest(ITestOutputHelper output)
    : RocksIndexTestBase<Address, long, NonceIndex>(output)
{
    protected override NonceIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override Address CreateKey(Random random) => RandomUtility.Address(random);

    protected override long CreateValue(Random random) => RandomUtility.Int64(random);
}
