using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteNonceIndexTest(ITestOutputHelper output)
    : LiteIndexTestBase<Address, long, NonceIndex>(output)
{
    protected override NonceIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override Address CreateKey(Random random) => RandomUtility.Address(random);

    protected override long CreateValue(Random random) => RandomUtility.Int64(random);
}
