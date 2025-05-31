using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryTxExecutionIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<TxId, TxExecution, TxExecutionIndex>(output)
{
    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override TxExecution CreateValue(Random random) => RandomUtility.TxExecution(random);
}
