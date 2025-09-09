using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.Tests;

public sealed class MemoryCommittedEvidenceIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<EvidenceId, EvidenceBase, CommittedEvidenceIndex>(output)
{
    protected override CommittedEvidenceIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override EvidenceId CreateKey(Random random) => Rand.EvidenceId(random);

    protected override EvidenceBase CreateValue(Random random) => Rand.Evidence(random);
}
