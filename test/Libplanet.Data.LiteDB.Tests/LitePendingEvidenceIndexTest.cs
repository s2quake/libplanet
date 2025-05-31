using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LitePendingEvidenceIndexTest(ITestOutputHelper output)
    : LiteKeyedIndexTestBase<EvidenceId, EvidenceBase, PendingEvidenceIndex>(output)
{
    protected override PendingEvidenceIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override EvidenceId CreateKey(Random random) => RandomUtility.EvidenceId(random);

    protected override EvidenceBase CreateValue(Random random) => RandomUtility.Evidence(random);
}
