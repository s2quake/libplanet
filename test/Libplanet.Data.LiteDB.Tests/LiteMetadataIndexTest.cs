using Libplanet.TestUtilities;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteMetadataIndexTest(ITestOutputHelper output)
    : LiteIndexTestBase<string, string, MetadataIndex>(output)
{
    protected override MetadataIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override string CreateKey(Random random) => RandomUtility.Word(random);

    protected override string CreateValue(Random random) => RandomUtility.String(random);
}
