
namespace Libplanet.Data.Tests;

public sealed class RepositoryTest : RepositoryTestBase<Repository>
{
    protected override Repository CreateRepository() => new();
}
