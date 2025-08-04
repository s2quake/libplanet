using Libplanet.TestUtilities;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryTxExecutionIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<TxId, TxExecution, TxExecutionIndex>(output)
{
    protected override TxExecutionIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override TxExecution CreateValue(Random random) => RandomUtility.TxExecution(random);
}
