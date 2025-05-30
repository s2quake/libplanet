
namespace Libplanet.Data.Tests;

public abstract class RepositoryTestBase<TRepository>
    where TRepository : Repository
{
    protected abstract TRepository CreateRepository();

    [Fact]
    public void Test()
    {
        var repository = CreateRepository();
        Assert.NotEqual(Guid.Empty, repository.Id);
        // for test coverage
        Assert.NotNull(repository.PendingEvidences);
        Assert.NotNull(repository.CommittedEvidences);
        Assert.NotNull(repository.PendingTransactions);
        Assert.NotNull(repository.CommittedTransactions);
        Assert.NotNull(repository.BlockCommits);
        Assert.NotNull(repository.BlockDigests);
        Assert.NotNull(repository.StateRootHashes);
        Assert.NotNull(repository.TxExecutions);
        Assert.NotNull(repository.BlockExecutions);
        Assert.NotNull(repository.BlockHashes);
        Assert.NotNull(repository.Nonces);
        Assert.NotNull(repository.States);
        Assert.Equal(-1, repository.GenesisHeight);
        Assert.Equal(-1, repository.Height);
        Assert.Equal(default, repository.StateRootHash);
        Assert.Throws<InvalidOperationException>(() => repository.GenesisBlockHash);
        Assert.Throws<InvalidOperationException>(() => repository.BlockHash);
        Assert.Throws<InvalidOperationException>(() => repository.BlockCommit);
    }
}
