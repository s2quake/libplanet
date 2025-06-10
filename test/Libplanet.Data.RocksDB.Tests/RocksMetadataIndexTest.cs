using Libplanet.TestUtilities;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksMetadataIndexTest(ITestOutputHelper output)
    : RocksIndexTestBase<string, string, MetadataIndex>(output)
{
    protected override MetadataIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override string CreateKey(Random random) => RandomUtility.Word(random);

    protected override string CreateValue(Random random) => RandomUtility.String(random);
}
