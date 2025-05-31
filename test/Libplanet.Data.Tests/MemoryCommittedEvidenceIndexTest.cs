using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryCommittedEvidenceIndexTest(ITestOutputHelper output)
    : MemoryKeyedIndexTestBase<EvidenceId, EvidenceBase, CommittedEvidenceIndex>(output)
{
    protected override EvidenceId CreateKey(Random random) => RandomUtility.EvidenceId(random);

    protected override EvidenceBase CreateValue(Random random) => RandomUtility.Evidence(random);
}
