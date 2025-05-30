using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class CommittedTransactionIndexTest(ITestOutputHelper output)
    : KeyedIndexTestBase<TxId, Transaction, CommittedTransactionIndex>(output)
{
    protected override CommittedTransactionIndex CreateIndex(bool useCache)
        => new(new MemoryDatabase(), useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override Transaction CreateValue(Random random) => RandomUtility.Transaction(random);
}
