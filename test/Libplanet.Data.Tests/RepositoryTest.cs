
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class RepositoryTest(ITestOutputHelper output) : RepositoryTestBase<Repository>(output)
{
    protected override Repository CreateRepository() => new();
}
