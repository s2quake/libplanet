using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests;

public partial class BlockchainTest
{
    [Fact]
    public void GetPendingEvidence_Test()
    {
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        Assert.Empty(blockchain.PendingEvidence);
    }

    [Fact]
    public void GetPendingEvidence_AfterAddingEvidence_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var height = blockchain.Tip.Height;
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(height, address, DateTimeOffset.UtcNow);

        // When
        blockchain.PendingEvidence.Add(testEvidence);

        // Then
        Assert.Single(blockchain.PendingEvidence);
    }

    [Fact]
    public void GetPendingEvidence_Throw_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var height = blockchain.Tip.Height;
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(height, address, DateTimeOffset.UtcNow);
        Assert.Throws<KeyNotFoundException>(() => blockchain.PendingEvidence[testEvidence.Id]);
    }

    [Fact]
    public void GetPendingEvidence_AfterAppendingBlock_Throw_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var expectedEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        blockchain.PendingEvidence.Add(expectedEvidence);

        // When
        blockchain.ProposeAndAppend(proposer);

        // Then
        Assert.Throws<KeyNotFoundException>(() => blockchain.PendingEvidence[expectedEvidence.Id]);
    }

    [Fact]
    public void GetPendingEvidence_Add_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var expectedEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        blockchain.PendingEvidence.Add(expectedEvidence);

        // Then
        var actualEvidence = blockchain.PendingEvidence[expectedEvidence.Id];
        Assert.Equal(expectedEvidence, actualEvidence);
    }

    [Fact]
    public void GetCommittedEvidence_Throw_Test()
    {
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var height = blockchain.Tip.Height;
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(height, address, DateTimeOffset.UtcNow);
        Assert.Throws<KeyNotFoundException>(() => blockchain.Evidence[testEvidence.Id]);
    }

    [Fact]
    public void GetCommittedEvidence_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var expectedEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        blockchain.PendingEvidence.Add(expectedEvidence);

        // When
        blockchain.ProposeAndAppend(proposer);

        // Then
        var actualEvidence = blockchain.Evidence[expectedEvidence.Id];
        Assert.Equal(expectedEvidence, actualEvidence);
    }

    [Fact]
    public void AddEvidence_CommittedEvidence_ThrowTest()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        blockchain.PendingEvidence.Add(testEvidence);

        // When
        blockchain.ProposeAndAppend(proposer);

        // Then
        Assert.Throws<ArgumentException>(() => blockchain.PendingEvidence.Add(testEvidence));
    }

    [Fact]
    public void AddEvidence_PendingEvidence_ThrowTest()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        blockchain.PendingEvidence.Add(testEvidence);

        // Then
        Assert.Throws<ArgumentException>(() => blockchain.PendingEvidence.Add(testEvidence));
    }

    [Fact]
    public void AddEvidence_HeightGreaterThanTip_ThrowTest()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var height = blockchain.Tip.Height + 1;
        var testEvidence = TestEvidence.Create(height, address, DateTimeOffset.UtcNow);

        // Then
        Assert.Throws<ArgumentException>(() => blockchain.PendingEvidence.Add(testEvidence));
    }

    [Fact]
    public void AddEvidence_ExpiredEvidence_ThrowTest()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var options = new BlockchainOptions();
        var blockchain = new Blockchain(genesisBlock, options);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        var pendingDuration = options.EvidenceOptions.ExpiresInBlocks;

        // When
        blockchain.ProposeAndAppendMany(proposer, pendingDuration + 1);

        // Then
        Assert.Throws<ArgumentException>(() => blockchain.PendingEvidence.Add(testEvidence));
    }

    [Fact]
    public void AddEvidence_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

        // When
        blockchain.PendingEvidence.Add(testEvidence);

        // Then
        Assert.Single(blockchain.PendingEvidence);
    }

    [Fact]
    public void CommitEvidence_AddingCommittedEvidence_ThrowTest()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        blockchain.PendingEvidence.Add(testEvidence);
        var (block1, blockCommit1) = blockchain.ProposeAndAppend(proposer);

        // When
        var block2 = new BlockBuilder
        {
            Height = block1.Height + 1,
            Timestamp = block1.Timestamp.AddSeconds(1),
            PreviousBlockHash = block1.BlockHash,
            PreviousBlockCommit = blockCommit1,
            PreviousStateRootHash = blockchain.StateRootHash,
            Evidence = [testEvidence],
        }.Create(proposer);
        var blockCommit2 = CreateBlockCommit(block2);

        // Then
        Assert.Throws<ArgumentException>(() => blockchain.Append(block2, blockCommit2));
    }

    [Fact]
    public void CommitEvidence_AddingExpiredEvidence_ThrowTest()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var options = new BlockchainOptions();
        var blockchain = new Blockchain(genesisBlock, options);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        var pendingDuration = options.EvidenceOptions.ExpiresInBlocks;
        var e = blockchain.ProposeAndAppendMany(proposer, pendingDuration + 1);
        var block1 = e[^1].Block;
        var blockCommit1 = e[^1].BlockCommit;

        // When
        var block2 = new BlockBuilder
        {
            Height = block1.Height + 1,
            Timestamp = block1.Timestamp.AddSeconds(1),
            PreviousBlockHash = block1.BlockHash,
            PreviousBlockCommit = blockCommit1,
            PreviousStateRootHash = blockchain.StateRootHash,
            Evidence = [testEvidence],
        }.Create(proposer);
        var blockCommit2 = CreateBlockCommit(block2);

        // Then
        Assert.Throws<ArgumentException>(() => blockchain.Append(block2, blockCommit2));
    }

    [Fact]
    public void CommitEvidence_WithoutPendingEvidence_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        var (block1, blockCommit1) = blockchain.ProposeAndAppend(proposer);
        var block2 = new BlockBuilder
        {
            Height = block1.Height + 1,
            Timestamp = block1.Timestamp.AddSeconds(1),
            PreviousBlockHash = block1.BlockHash,
            PreviousBlockCommit = blockCommit1,
            PreviousStateRootHash = blockchain.StateRootHash,
            Evidence = [testEvidence],
        }.Create(proposer);
        var blockCommit2 = CreateBlockCommit(block2);

        // When
        blockchain.Append(block2, blockCommit2);

        // Then
        Assert.Contains(testEvidence.Id, blockchain.Evidence);
    }

    [Fact]
    public void CommitEvidence_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

        // When
        blockchain.PendingEvidence.Add(testEvidence);
        blockchain.ProposeAndAppend(proposer);

        // Then
        Assert.Empty(blockchain.PendingEvidence);
        Assert.Contains(testEvidence.Id, blockchain.Evidence);
    }

    [Fact]
    public void IsEvidencePending_True_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        blockchain.PendingEvidence.Add(testEvidence);

        // Then
        Assert.Contains(testEvidence.Id, blockchain.PendingEvidence);
    }

    [Fact]
    public void IsEvidencePending_False_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

        // Then
        Assert.DoesNotContain(testEvidence.Id, blockchain.PendingEvidence);
    }

    [Fact]
    public void IsEvidenceCommitted_True_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        blockchain.PendingEvidence.Add(testEvidence);

        // When
        blockchain.ProposeAndAppend(proposer);

        // Then
        Assert.Contains(testEvidence.Id, blockchain.Evidence);
    }

    [Fact]
    public void IsEvidenceCommitted_False_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        blockchain.PendingEvidence.Add(testEvidence);

        // Then
        Assert.DoesNotContain(testEvidence.Id, blockchain.Evidence);
    }

    [Fact]
    public void DeletePendingEvidence_True_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
        blockchain.PendingEvidence.Add(testEvidence);

        // Then
        Assert.True(blockchain.PendingEvidence.Remove(testEvidence.Id));
    }

    [Fact]
    public void DeletePendingEvidence_False_Test()
    {
        // Given
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var address = Rand.Address(random);
        var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

        // Then
        Assert.False(blockchain.PendingEvidence.Remove(testEvidence.Id));
        Assert.DoesNotContain(testEvidence.Id, blockchain.PendingEvidence);
        Assert.DoesNotContain(testEvidence.Id, blockchain.Evidence);
    }

    [Fact]
    public void AddEvidence_CommitEvidence_DuplicatedVoteEvidence_Test()
    {
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        _ = blockchain.ProposeAndAppend(proposer);
        var voteRef = new VoteMetadata
        {
            Height = blockchain.Tip.Height,
            Round = 2,
            BlockHash = new BlockHash(Rand.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = Signers[0].Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(Signers[0]);
        var voteDup = new VoteMetadata
        {
            Height = blockchain.Tip.Height,
            Round = 2,
            BlockHash = new BlockHash(Rand.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = Signers[0].Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(Signers[0]);
        var evidence = DuplicateVoteEvidence.Create(voteRef, voteDup, TestUtils.Validators);

        Assert.Empty(blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.Evidence);

        blockchain.PendingEvidence.Add(evidence);
        var block1 = new BlockBuilder
        {
            Height = blockchain.Tip.Height + 1,
            PreviousBlockHash = blockchain.Tip.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit1 = CreateBlockCommit(block1);
        blockchain.Append(block1, blockCommit1);

        Assert.Single(blockchain.PendingEvidence.Keys, evidence.Id);
        Assert.Contains(evidence.Id, blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.Evidence);

        blockchain.ProposeAndAppend(proposer);

        Assert.Empty(blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.PendingEvidence);
        Assert.Contains(evidence.Id, blockchain.Evidence);
    }

    [Fact]
    public void CommitEvidence_DuplicateVoteEvidence_Test()
    {
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        _ = blockchain.ProposeAndAppend(proposer);
        var voteRef = new VoteMetadata
        {
            Height = blockchain.Tip.Height,
            Round = 2,
            BlockHash = new BlockHash(Rand.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = Signers[0].Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(Signers[0]);
        var voteDup = new VoteMetadata
        {
            Height = blockchain.Tip.Height,
            Round = 2,
            BlockHash = new BlockHash(Rand.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = Signers[0].Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(Signers[0]);
        var evidence = DuplicateVoteEvidence.Create(voteRef, voteDup, TestUtils.Validators);

        Assert.Empty(blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.Evidence);

        var block1 = new BlockBuilder
        {
            Height = blockchain.Tip.Height + 1,
            Timestamp = blockchain.Tip.Timestamp.AddSeconds(1),
            PreviousBlockHash = blockchain.Tip.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
            Evidence = [evidence]
        }.Create(proposer);
        var blockCommit1 = CreateBlockCommit(block1);
        blockchain.Append(block1, blockCommit1);

        Assert.Empty(blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.PendingEvidence);
        Assert.Contains(evidence.Id, blockchain.Evidence);
    }

    [Fact]
    public void AddEvidence_DuplicateVoteEvidence_FromNonValidator_ThrowTest()
    {
        var random = Rand.GetRandom(_output);
        var signer = Rand.Signer(random);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        _ = blockchain.ProposeAndAppend(proposer);
        var voteRef = new VoteMetadata
        {
            Height = blockchain.Tip.Height,
            Round = 2,
            BlockHash = new BlockHash(Rand.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = signer.Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);
        var voteDup = new VoteMetadata
        {
            Height = blockchain.Tip.Height,
            Round = 2,
            BlockHash = new BlockHash(Rand.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = signer.Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);
        var validators = ImmutableSortedSet.Create(new Validator { Address = signer.Address });
        var evidence = DuplicateVoteEvidence.Create(voteRef, voteDup, validators);

        Assert.Empty(blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.Evidence);

        Assert.Throws<ArgumentException>(() => blockchain.PendingEvidence.Add(evidence));

        Assert.Empty(blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.Evidence);
    }

    [Fact]
    public void EvidenceExpired_ThrowTest()
    {
        var random = Rand.GetRandom(_output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var options = new BlockchainOptions();
        var blockchain = new Blockchain(genesisBlock);
        _ = blockchain.ProposeAndAppend(proposer);
        var voteRef = new VoteMetadata
        {
            Height = blockchain.Tip.Height,
            Round = 2,
            BlockHash = new BlockHash(Rand.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = Signers[0].Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(Signers[0]);
        var voteDup = new VoteMetadata
        {
            Height = blockchain.Tip.Height,
            Round = 2,
            BlockHash = new BlockHash(Rand.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = Signers[0].Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(Signers[0]);
        var evidence = DuplicateVoteEvidence.Create(voteRef, voteDup, TestUtils.Validators);

        Assert.Empty(blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.Evidence);

        blockchain.PendingEvidence.Add(evidence);

        Assert.Single(blockchain.PendingEvidence.Keys, evidence.Id);
        Assert.Contains(evidence.Id, blockchain.PendingEvidence);
        Assert.DoesNotContain(evidence.Id, blockchain.Evidence);

        var pendingDuration = options.EvidenceOptions.ExpiresInBlocks;

        for (var i = 0; i < pendingDuration; i++)
        {
            var block1 = new BlockBuilder
            {
                Height = blockchain.Tip.Height + 1,
                PreviousBlockHash = blockchain.Tip.BlockHash,
                PreviousStateRootHash = blockchain.StateRootHash,
            }.Create(proposer);
            var blockCommit1 = CreateBlockCommit(block1);
            blockchain.Append(block1, blockCommit1);
        }

        var block2 = new BlockBuilder
        {
            Height = blockchain.Tip.Height + 1,
            PreviousBlockHash = blockchain.Tip.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
            Evidence = [evidence],
        }.Create(proposer);
        var blockCommit2 = CreateBlockCommit(block2);
        Assert.Throws<ArgumentException>(() => blockchain.Append(block2, blockCommit2));

        Assert.Single(blockchain.PendingEvidence);

        blockchain.ProposeAndAppend(proposer);
        Assert.Single(blockchain.PendingEvidence);
        blockchain.PendingEvidence.Prune();
        Assert.Empty(blockchain.PendingEvidence);
    }
}
