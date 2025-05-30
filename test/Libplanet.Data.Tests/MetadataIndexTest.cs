using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MetadataIndexTest(ITestOutputHelper output)
    : IndexTestBase<string, string>(output)
{
    protected override IndexBase<string, string> CreateIndex(bool useCache)
        => new MetadataIndex(new MemoryDatabase(), useCache ? 100 : 0);

    protected override string CreateKey(Random random) => RandomUtility.Word(random);

    protected override string CreateValue(Random random) => RandomUtility.String(random);
}
