using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksTxExecutionIndexTest(ITestOutputHelper output)
    : RocksKeyedIndexTestBase<TxId, TxExecution, TxExecutionIndex>(output)
{
    protected override TxExecutionIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override TxExecution CreateValue(Random random) => RandomUtility.TxExecution(random);
}
