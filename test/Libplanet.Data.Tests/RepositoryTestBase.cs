
using System.Reflection;
using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public abstract class RepositoryTestBase<TRepository>(ITestOutputHelper output)
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
        Assert.Equal(default, repository.GenesisBlockHash);
        Assert.Equal(default, repository.BlockHash);
        Assert.Equal(BlockCommit.Empty, repository.BlockCommit);
    }

    [Fact]
    public void ExistedRepository()
    {
        var random = GetRandom();
        var repository1 = CreateRepository();
        repository1.GenesisHeight = RandomUtility.NonNegative(random);
        repository1.Height = RandomUtility.NonNegative(random);
        repository1.StateRootHash = RandomUtility.HashDigest<SHA256>(random);

        var propertyInfo = repository1.GetType().GetProperty("Database", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var database = (IDatabase)propertyInfo.GetValue(repository1)!;
        var repository2 = new Repository(database);

        Assert.Equal(repository1.Id, repository2.Id);
        Assert.Equal(repository1.GenesisHeight, repository2.GenesisHeight);
        Assert.Equal(repository1.Height, repository2.Height);
        Assert.Equal(repository1.StateRootHash, repository2.StateRootHash);
    }

    [Fact]
    public void GenesisHeight()
    {
        var random = GetRandom();
        var repository = CreateRepository();
        var genesisHeight = RandomUtility.NonNegative(random);
        repository.GenesisHeight = genesisHeight;

        Assert.Equal(genesisHeight, repository.GenesisHeight);
        Assert.Equal(genesisHeight, repository.BlockHashes.GenesisHeight);
        Assert.Throws<KeyNotFoundException>(() => repository.GenesisBlockHash);

        repository.GenesisHeight = -1;

        Assert.Equal(-1, repository.GenesisHeight);
        Assert.Equal(-1, repository.BlockHashes.GenesisHeight);
        Assert.Equal(default, repository.GenesisBlockHash);

        Assert.Throws<ArgumentOutOfRangeException>(() => repository.GenesisHeight = -2);
    }

    [Fact]
    public void Height()
    {
        var random = GetRandom();
        var repository = CreateRepository();
        var height = RandomUtility.NonNegative(random);
        repository.Height = height;

        Assert.Equal(height, repository.Height);
        Assert.Equal(height, repository.BlockHashes.Height);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockHash);

        repository.Height = -1;

        Assert.Equal(-1, repository.Height);
        Assert.Equal(-1, repository.BlockHashes.Height);
        Assert.Equal(default, repository.BlockHash);

        Assert.Throws<ArgumentOutOfRangeException>(() => repository.Height = -2);
    }

    [Fact]
    public void StateRootHash()
    {
        var random = GetRandom();
        var repository = CreateRepository();
        var stateRootHash = RandomUtility.HashDigest<SHA256>(random);
        repository.StateRootHash = stateRootHash;

        Assert.Equal(stateRootHash, repository.StateRootHash);

        repository.StateRootHash = default;

        Assert.Equal(default, repository.StateRootHash);
    }

    [Fact]
    public void Append()
    {
        var random = GetRandom();
        var repository = CreateRepository();
        var block1 = RandomUtility.Block(random);
        var block2 = RandomUtility.Block(random);
        var blockCommit1 = new BlockCommit
        {
            BlockHash = block1.BlockHash,
            Height = block1.Height,
        };

        repository.Append(block1, blockCommit1);

        Assert.Equal(-1, repository.GenesisHeight);
        Assert.Equal(-1, repository.Height);
        Assert.Equal(default, repository.StateRootHash);
        Assert.Equal(default, repository.GenesisBlockHash);
        Assert.Equal(default, repository.BlockHash);
        Assert.Equal(BlockCommit.Empty, repository.BlockCommit);

        Assert.Contains(block1.BlockHash, repository.BlockDigests.Keys);
        Assert.Contains(block1.BlockHash, repository.BlockCommits.Keys);
        Assert.Contains(block1.Height, repository.BlockHashes.Keys);

        foreach (var item in block1.Transactions)
        {
            Assert.DoesNotContain(item.Id, repository.PendingTransactions.Keys);
            Assert.Contains(item.Id, repository.CommittedTransactions.Keys);

            Assert.NotEqual(0, repository.Nonces[item.Signer]);
        }

        foreach (var item in block1.Evidences)
        {
            Assert.DoesNotContain(item.Id, repository.PendingEvidences.Keys);
            Assert.Contains(item.Id, repository.CommittedEvidences.Keys);
        }

        repository.Append(block2, BlockCommit.Empty);
        Assert.DoesNotContain(block2.BlockHash, repository.BlockCommits.Keys);
    }

    [Fact]
    public void Append_Throw()
    {
        var random = GetRandom();
        var repository = CreateRepository();
        var block1 = RandomUtility.Block(random);
        var block2 = RandomUtility.Try(random, RandomUtility.Block, item => item.Height != block1.Height);
        var blockCommit1 = new BlockCommit
        {
            Height = block1.Height,
            BlockHash = RandomUtility.BlockHash(random),
        };
        var blockCommit2 = new BlockCommit
        {
            Height = RandomUtility.Try(random, RandomUtility.Int32, item => item != block2.Height),
            BlockHash = block2.BlockHash,
        };

        Assert.Throws<ArgumentException>("blockCommit", () => repository.Append(block1, blockCommit1));
        Assert.Throws<ArgumentException>("blockCommit", () => repository.Append(block2, blockCommit2));
    }

    [Fact]
    public void GetBlock_ByBlockHash()
    {
        var random = GetRandom();
        var repository = CreateRepository();
        var block = RandomUtility.Block(random);

        repository.Append(block, BlockCommit.Empty);

        var actualBlock = repository.GetBlock(block.BlockHash);
        Assert.Equal(block, actualBlock);

        Assert.Throws<KeyNotFoundException>(() => repository.GetBlock(RandomUtility.BlockHash(random)));
    }

    [Fact]
    public void GetBlock_ByHeight()
    {
        var random = GetRandom();
        var repository = CreateRepository();
        var block = RandomUtility.Block(random);

        repository.Append(block, BlockCommit.Empty);

        var actualBlock = repository.GetBlock(block.Height);
        Assert.Equal(block, actualBlock);

        var nonExistentHeight = RandomUtility.Try(random, RandomUtility.NonNegative, item => item != block.Height);
        Assert.Throws<KeyNotFoundException>(() => repository.GetBlock(nonExistentHeight));
    }

    protected Random GetRandom()
    {
        var seed = RandomUtility.Int32();
        output.WriteLine($"Random seed: {seed}");
        return new Random(seed);
    }
}
