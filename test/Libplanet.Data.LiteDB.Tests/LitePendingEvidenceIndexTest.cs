using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LitePendingEvidenceIndexTest(ITestOutputHelper output)
    : LiteKeyedIndexTestBase<EvidenceId, EvidenceBase, PendingEvidenceIndex>(output)
{
    protected override PendingEvidenceIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override EvidenceId CreateKey(Random random) => Rand.EvidenceId(random);

    protected override EvidenceBase CreateValue(Random random) => Rand.Evidence(random);
}
