using Libplanet.TestUtilities;
using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksCommittedTransactionIndexTest(ITestOutputHelper output)
    : RocksKeyedIndexTestBase<TxId, Transaction, CommittedTransactionIndex>(output)
{
    protected override CommittedTransactionIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override Transaction CreateValue(Random random) => RandomUtility.Transaction(random);
}
