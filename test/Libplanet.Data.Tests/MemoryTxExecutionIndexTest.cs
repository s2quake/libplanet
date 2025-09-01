using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.Tests;

public sealed class MemoryTxExecutionIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<TxId, TxExecutionInfo, TxExecutionIndex>(output)
{
    protected override TxExecutionIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override TxExecutionInfo CreateValue(Random random) => RandomUtility.TxExecution(random);
}
