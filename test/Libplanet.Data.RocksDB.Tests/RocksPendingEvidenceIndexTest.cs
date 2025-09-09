using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksPendingEvidenceIndexTest(ITestOutputHelper output)
    : RocksKeyedIndexTestBase<EvidenceId, EvidenceBase, PendingEvidenceIndex>(output)
{
    protected override PendingEvidenceIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override EvidenceId CreateKey(Random random) => Rand.EvidenceId(random);

    protected override EvidenceBase CreateValue(Random random) => Rand.Evidence(random);
}
