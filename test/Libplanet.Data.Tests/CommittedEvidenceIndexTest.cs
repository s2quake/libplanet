using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class CommittedEvidenceIndexTest(ITestOutputHelper output)
    : KeyedIndexTestBase<EvidenceId, EvidenceBase>(output)
{
    protected override KeyedIndexBase<EvidenceId, EvidenceBase> CreateIndex(bool useCache)
        => new CommittedEvidenceIndex(new MemoryDatabase(), useCache ? 100 : 0);

    protected override EvidenceId CreateKey(Random random) => RandomUtility.EvidenceId(random);

    protected override EvidenceBase CreateValue(Random random) => RandomUtility.Evidence(random);
}
