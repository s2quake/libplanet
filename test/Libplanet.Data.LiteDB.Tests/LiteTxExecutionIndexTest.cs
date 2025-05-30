using Libplanet.Data.Tests;
using Xunit.Abstractions;
using static Libplanet.Data.LiteDB.Tests.LiteDatabaseUtility;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteTxExecutionIndexTest(ITestOutputHelper output) : TxExecutionIndexTest(output)
{
    protected override TxExecutionIndex CreateIndex(string name, bool useCache)
        => new(CreateDatabase(this, name), useCache ? 100 : 0);
}
