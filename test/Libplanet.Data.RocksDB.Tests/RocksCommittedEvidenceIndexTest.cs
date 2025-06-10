using Libplanet.TestUtilities;
using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksCommittedEvidenceIndexTest(ITestOutputHelper output)
    : RocksKeyedIndexTestBase<EvidenceId, EvidenceBase, CommittedEvidenceIndex>(output)
{
    protected override CommittedEvidenceIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override EvidenceId CreateKey(Random random) => RandomUtility.EvidenceId(random);

    protected override EvidenceBase CreateValue(Random random) => RandomUtility.Evidence(random);
}
