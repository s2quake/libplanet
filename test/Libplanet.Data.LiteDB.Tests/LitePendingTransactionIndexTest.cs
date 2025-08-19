using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LitePendingTransactionIndexTest(ITestOutputHelper output)
    : LiteKeyedIndexTestBase<TxId, Transaction, PendingTransactionIndex>(output)
{
    protected override PendingTransactionIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override Transaction CreateValue(Random random) => RandomUtility.Transaction(random);
}
