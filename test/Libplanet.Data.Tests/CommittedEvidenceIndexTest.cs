using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public class CommittedEvidenceIndexTest(ITestOutputHelper output)
    : KeyedIndexTestBase<EvidenceId, EvidenceBase, CommittedEvidenceIndex>(output)
{
    protected override CommittedEvidenceIndex CreateIndex(bool useCache)
        => new(new MemoryDatabase(), useCache ? 100 : 0);

    protected override EvidenceId CreateKey(Random random) => RandomUtility.EvidenceId(random);

    protected override EvidenceBase CreateValue(Random random) => RandomUtility.Evidence(random);
}
