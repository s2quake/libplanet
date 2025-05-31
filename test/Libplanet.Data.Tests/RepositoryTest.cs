using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class RepositoryTest(ITestOutputHelper output) : RepositoryTestBase<Repository>(output)
{
    protected override Repository CreateRepository() => new();

    [Fact]
    public void RepositoryGeneric()
    {
        var database = new MemoryDatabase();
        var repository = new Repository<MemoryDatabase>(database);
        Assert.Equal(database, repository.Database);
    }
}
