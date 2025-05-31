using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryPendingTransactionIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<TxId, Transaction, PendingTransactionIndex>(output)
{
    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override Transaction CreateValue(Random random) => RandomUtility.Transaction(random);
}
