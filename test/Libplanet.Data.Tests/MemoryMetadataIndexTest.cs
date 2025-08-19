using Libplanet.TestUtilities;

namespace Libplanet.Data.Tests;

public sealed class MemoryMetadataIndexTest(ITestOutputHelper output)
    : MemoryIndexTestBase<string, string, MetadataIndex>(output)
{
    protected override MetadataIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override string CreateKey(Random random) => RandomUtility.Word(random);

    protected override string CreateValue(Random random) => RandomUtility.String(random);
}
