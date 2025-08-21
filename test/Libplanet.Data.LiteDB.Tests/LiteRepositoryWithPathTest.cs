using System.IO;
using Libplanet.Data.Tests;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteRepositoryWithPath(ITestOutputHelper output) : RepositoryTestBase<LiteRepository>(output)
{
    protected override LiteRepository CreateRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), $"{nameof(LiteRepositoryWithPath)}");
        return new LiteRepository(path);
    }
}
