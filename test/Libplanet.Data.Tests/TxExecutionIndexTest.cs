using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class TxExecutionIndexTest(ITestOutputHelper output)
    : KeyedIndexTestBase<TxId, TxExecution>(output)
{
    protected override KeyedIndexBase<TxId, TxExecution> CreateIndex(bool useCache)
        => new TxExecutionIndex(new MemoryDatabase(), useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override TxExecution CreateValue(Random random) => RandomUtility.TxExecution(random);
}
