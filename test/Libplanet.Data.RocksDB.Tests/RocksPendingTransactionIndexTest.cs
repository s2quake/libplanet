using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksPendingTransactionIndexTest(ITestOutputHelper output)
    : RocksKeyedIndexTestBase<TxId, Transaction, PendingTransactionIndex>(output)
{
    protected override PendingTransactionIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => Rand.TxId(random);

    protected override Transaction CreateValue(Random random) => Rand.Transaction(random);
}
