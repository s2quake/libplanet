using Libplanet.Data.Tests;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteRepositoryTest(ITestOutputHelper output) : RepositoryTestBase<LiteRepository>(output)
{
    protected override LiteRepository CreateRepository() => new();
}
