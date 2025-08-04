using Libplanet.TestUtilities;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteTxExecutionIndexTest(ITestOutputHelper output)
    : LiteKeyedIndexTestBase<TxId, TxExecution, TxExecutionIndex>(output)
{
    protected override TxExecutionIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override TxId CreateKey(Random random) => RandomUtility.TxId(random);

    protected override TxExecution CreateValue(Random random) => RandomUtility.TxExecution(random);
}
