using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryCommittedTransactionIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<TxId, Transaction, CommittedTransactionIndex>(output)
{
    protected override CommittedTransactionIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override Transaction CreateValue(Random random) => RandomUtility.Transaction(random);
}
