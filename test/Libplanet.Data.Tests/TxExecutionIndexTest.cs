using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public class TxExecutionIndexTest(ITestOutputHelper output)
    : KeyedIndexTestBase<TxId, TxExecution, TxExecutionIndex>(output)
{
    protected override TxExecutionIndex CreateIndex(bool useCache)
        => new(new MemoryDatabase(), useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override TxExecution CreateValue(Random random) => RandomUtility.TxExecution(random);
}
