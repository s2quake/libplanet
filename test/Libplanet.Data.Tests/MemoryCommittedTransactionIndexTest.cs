using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.Tests;

public sealed class MemoryCommittedTransactionIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<TxId, Transaction, CommittedTransactionIndex>(output)
{
    protected override CommittedTransactionIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => Rand.TxId(random);

    protected override Transaction CreateValue(Random random) => Rand.Transaction(random);
}
