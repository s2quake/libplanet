using System.IO;
using Libplanet.Data.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteRepositoryWithPath(ITestOutputHelper output) : RepositoryTestBase<LiteRepository>(output)
{
    protected override LiteRepository CreateRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{nameof(LiteRepositoryWithPath)}");
        return new LiteRepository(path);
    }
}
