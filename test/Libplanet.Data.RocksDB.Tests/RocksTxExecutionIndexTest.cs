using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksTxExecutionIndexTest(ITestOutputHelper output)
    : RocksKeyedIndexTestBase<TxId, TransactionExecutionInfo, TxExecutionIndex>(output)
{
    protected override TxExecutionIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override TransactionExecutionInfo CreateValue(Random random) => RandomUtility.TxExecution(random);
}
