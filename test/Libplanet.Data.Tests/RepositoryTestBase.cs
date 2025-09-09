
using System.Reflection;
using System.Security.Cryptography;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Libplanet.Types.Progresses;

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
        Assert.True(repository.PendingEvidences.IsEmpty);
        Assert.True(repository.CommittedEvidences.IsEmpty);
        Assert.True(repository.PendingTransactions.IsEmpty);
        Assert.True(repository.CommittedTransactions.IsEmpty);
        Assert.True(repository.BlockCommits.IsEmpty);
        Assert.True(repository.BlockDigests.IsEmpty);
        Assert.True(repository.StateRootHashes.IsEmpty);
        Assert.True(repository.TxExecutions.IsEmpty);
        Assert.True(repository.BlockExecutions.IsEmpty);
        Assert.True(repository.BlockHashes.IsEmpty);
        Assert.True(repository.Nonces.IsEmpty);
        Assert.True(repository.States.IsEmpty);
        Assert.True(repository.IsEmpty);
        Assert.Equal(-1, repository.GenesisHeight);
        Assert.Equal(-1, repository.Height);
        Assert.Equal(default, repository.StateRootHash);
        Assert.Equal(default, repository.GenesisBlockHash);
        Assert.Equal(default, repository.BlockHash);
        Assert.Equal(default, repository.BlockCommit);
        Assert.Equal(default, repository.Timestamp);
        Assert.Equal(BlockHeader.CurrentVersion, repository.BlockVersion);
    }

    [Fact]
    public void ExistedRepository()
    {
        var random = Rand.GetRandom(output);
        var repository1 = CreateRepository();
        repository1.GenesisHeight = Rand.NonNegative(random);
        repository1.Height = Rand.NonNegative(random);
        repository1.StateRootHash = Rand.HashDigest<SHA256>(random);

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
        var random = Rand.GetRandom(output);
        var repository = CreateRepository();
        var genesisHeight = Rand.NonNegative(random);
        repository.GenesisHeight = genesisHeight;

        Assert.Equal(genesisHeight, repository.GenesisHeight);
        Assert.Throws<KeyNotFoundException>(() => repository.GenesisBlockHash);

        repository.GenesisHeight = -1;

        Assert.Equal(-1, repository.GenesisHeight);
        Assert.Equal(default, repository.GenesisBlockHash);

        Assert.Throws<ArgumentOutOfRangeException>(() => repository.GenesisHeight = -2);

        var blockHash = Rand.BlockHash(random);
        repository.GenesisHeight = 0;
        repository.BlockHashes[0] = blockHash;
        Assert.Equal(blockHash, repository.GenesisBlockHash);
    }

    [Fact]
    public void Height()
    {
        var random = Rand.GetRandom(output);
        var repository = CreateRepository();
        var height = Rand.NonNegative(random);
        repository.Height = height;

        Assert.Equal(height, repository.Height);
        Assert.Equal(height, repository.BlockHashes.Height);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockHash);

        repository.Height = -1;

        Assert.Equal(-1, repository.Height);
        Assert.Equal(-1, repository.BlockHashes.Height);
        Assert.Equal(default, repository.BlockHash);

        Assert.Throws<ArgumentOutOfRangeException>(() => repository.Height = -2);

        var blockHash = Rand.BlockHash(random);
        repository.Height = 0;
        repository.BlockHashes[0] = blockHash;
        Assert.Equal(blockHash, repository.BlockHash);
    }

    [Fact]
    public void StateRootHash()
    {
        var random = Rand.GetRandom(output);
        var repository = CreateRepository();
        var stateRootHash = Rand.HashDigest<SHA256>(random);
        repository.StateRootHash = stateRootHash;

        Assert.Equal(stateRootHash, repository.StateRootHash);

        repository.StateRootHash = default;

        Assert.Equal(default, repository.StateRootHash);
    }

    [Fact]
    public void Append()
    {
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var repository = CreateRepository();

        var block1 = new BlockBuilder
        {
            Height = Rand.NonNegative(random),
            Transactions =
            [
                new TransactionBuilder
                {
                    Nonce = 0L,
                }.Create(proposer),
                new TransactionBuilder
                {
                    Nonce = 1L,
                    Actions = [new TestAction()],
                }.Create(proposer),
            ],
            Evidence =
            [
                TestEvidence.Create(0, Rand.Address(random), DateTimeOffset.UtcNow),
                TestEvidence.Create(0, Rand.Address(random), DateTimeOffset.UtcNow),
            ],
        }.Create(proposer);

        Assert.Equal(-1, repository.GenesisHeight);
        Assert.Equal(-1, repository.Height);
        Assert.Equal(default, repository.StateRootHash);
        Assert.Equal(default, repository.GenesisBlockHash);
        Assert.Equal(default, repository.BlockHash);
        Assert.Equal(default, repository.BlockCommit);

        repository.Append(block1, default);

        Assert.Equal(block1.Height, repository.GenesisHeight);
        Assert.Equal(block1.Height, repository.Height);
        Assert.Equal(default, repository.StateRootHash);
        Assert.Equal(block1.BlockHash, repository.GenesisBlockHash);
        Assert.Equal(block1.BlockHash, repository.BlockHash);
        Assert.Equal(default, repository.BlockCommit);

        Assert.Contains(block1.BlockHash, repository.BlockDigests.Keys);
        Assert.Empty(repository.BlockCommits.Keys);
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

        var block2 = new BlockBuilder
        {
            Height = block1.Height + 1,
            PreviousBlockHash = block1.BlockHash,
            PreviousStateRootHash = repository.StateRootHash,
        }.Create(proposer);
        var blockCommit2 = new BlockCommit
        {
            Height = block2.Height,
            BlockHash = block2.BlockHash,
        };

        repository.Append(block2, blockCommit2);

        Assert.Equal(block1.Height, repository.GenesisHeight);
        Assert.Equal(block2.Height, repository.Height);
        Assert.Equal(default, repository.StateRootHash);
        Assert.Equal(block1.BlockHash, repository.GenesisBlockHash);
        Assert.Equal(block2.BlockHash, repository.BlockHash);
        Assert.Equal(blockCommit2, repository.BlockCommit);

        Assert.Contains(block2.BlockHash, repository.BlockDigests.Keys);
        Assert.Single(repository.BlockCommits.Keys, block2.BlockHash);
        Assert.Contains(block2.Height, repository.BlockHashes.Keys);
    }

    [Fact]
    public void Append_Throw()
    {
        var random = Rand.GetRandom(output);
        var repository = CreateRepository();
        var block1 = Rand.Block(random);
        var block2 = Rand.Try(random, Rand.Block, item => item.Height != block1.Height);
        var blockCommit1 = new BlockCommit
        {
            Height = block1.Height,
            BlockHash = Rand.BlockHash(random),
        };
        var blockCommit2 = new BlockCommit
        {
            Height = Rand.Try(random, Rand.Int32, item => item != block2.Height),
            BlockHash = block2.BlockHash,
        };

        Assert.Throws<ArgumentException>("blockCommit", () => repository.Append(block1, blockCommit1));
        Assert.Throws<ArgumentException>("blockCommit", () => repository.Append(block2, blockCommit2));
    }

    [Fact]
    public void GetBlock_ByBlockHash()
    {
        var random = Rand.GetRandom(output);
        var repository = CreateRepository();
        var block = new BlockBuilder
        {
            Height = Rand.NonNegative(random),
        }.Create(Rand.Signer(random));

        repository.Append(block, default);

        var actualBlock1 = repository.GetBlock(block.BlockHash);
        Assert.Equal(block, actualBlock1);
        Assert.True(repository.TryGetBlock(block.BlockHash, out var actualBlock2));
        Assert.Equal(block, actualBlock2);
        var actualBlock3 = repository.GetBlockOrDefault(block.BlockHash);
        Assert.Equal(block, actualBlock3);

        var nonExistentBlockHash = Rand.BlockHash(random);
        Assert.Throws<KeyNotFoundException>(() => repository.GetBlock(nonExistentBlockHash));
        Assert.False(repository.TryGetBlock(nonExistentBlockHash, out _));
        Assert.Null(repository.GetBlockOrDefault(nonExistentBlockHash));
    }

    [Fact]
    public void GetBlock_ByHeight()
    {
        var random = Rand.GetRandom(output);
        var repository = CreateRepository();
        var block = new BlockBuilder
        {
            Height = Rand.NonNegative(random),
        }.Create(Rand.Signer(random));

        repository.Append(block, default);

        var actualBlock1 = repository.GetBlock(block.Height);
        Assert.Equal(block, actualBlock1);
        Assert.True(repository.TryGetBlock(block.Height, out var actualBlock2));
        Assert.Equal(block, actualBlock2);
        var actualBlock3 = repository.GetBlockOrDefault(block.Height);
        Assert.Equal(block, actualBlock3);

        var nonExistentHeight = Rand.Try(random, Rand.NonNegative, item => item != block.Height);
        Assert.Throws<KeyNotFoundException>(() => repository.GetBlock(nonExistentHeight));
        Assert.False(repository.TryGetBlock(nonExistentHeight, out _));
        Assert.Null(repository.GetBlockOrDefault(nonExistentHeight));
    }

    [Fact]
    public void GetNonce()
    {
        var random = Rand.GetRandom(output);
        var repository = CreateRepository();
        var address = Rand.Address(random);
        var nonce = Rand.Int32(random);

        Assert.Equal(0, repository.GetNonce(address));

        repository.Nonces[address] = nonce;
        Assert.Equal(nonce, repository.GetNonce(address));

        Assert.Equal(0, repository.GetNonce(Rand.Address(random)));
    }

    [Fact]
    public async Task CopyToAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var repositoryA = CreateRepository();
        var repositoryB = CreateRepository();

        var block1 = new BlockBuilder
        {
            Height = 0,
            Transactions =
            [
                new TransactionBuilder
                {
                    Nonce = 0L,
                }.Create(proposer),
                new TransactionBuilder
                {
                    Nonce = 1L,
                    Actions = [new TestAction()],
                }.Create(proposer),
            ],
            Evidence =
            [
                TestEvidence.Create(0, Rand.Address(random), DateTimeOffset.UtcNow),
                TestEvidence.Create(0, Rand.Address(random), DateTimeOffset.UtcNow),
            ],
        }.Create(proposer);

        repositoryA.Append(block1, default);

        await repositoryA.CopyToAsync(repositoryB, cancellationToken, new Progress<ProgressInfo>());

        Assert.Equal(repositoryA.GenesisHeight, repositoryB.GenesisHeight);
        Assert.Equal(repositoryA.Height, repositoryB.Height);
        Assert.Equal(repositoryA.StateRootHash, repositoryB.StateRootHash);
        Assert.Equal(
            repositoryA.BlockDigests[repositoryA.BlockHash],
            repositoryB.BlockDigests[repositoryB.BlockHash]);
    }
}
