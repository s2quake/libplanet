using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteCommittedTransactionIndexTest(ITestOutputHelper output)
    : LiteKeyedIndexTestBase<TxId, Transaction, CommittedTransactionIndex>(output)
{
    protected override CommittedTransactionIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override Transaction CreateValue(Random random) => RandomUtility.Transaction(random);
}
