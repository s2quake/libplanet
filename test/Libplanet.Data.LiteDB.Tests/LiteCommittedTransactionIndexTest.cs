using Libplanet.Data.Tests;
using Xunit.Abstractions;
using static Libplanet.Data.LiteDB.Tests.LiteDatabaseUtility;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteCommittedTransactionIndexTest(ITestOutputHelper output) : CommittedTransactionIndexTest(output)
{
    protected override CommittedTransactionIndex CreateIndex(string name, bool useCache)
        => new(CreateDatabase(this, name), useCache ? 100 : 0);
}
