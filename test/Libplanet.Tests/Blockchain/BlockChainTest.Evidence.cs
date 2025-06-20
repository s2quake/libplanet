namespace Libplanet.Tests.Blockchain;

public partial class BlockChainTest
{
    // [Fact]
    // public void GetPendingEvidence_Test()
    // {
    //     Assert.Empty(_blockChain.PendingEvidences);
    // }

    // [Fact]
    // public void GetPendingEvidence_AfterAddingEvidence_Test()
    // {
    //     // Given
    //     var blockChain = _blockChain;
    //     var height = blockChain.Tip.Height;
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(height, address, DateTimeOffset.UtcNow);

    //     // When
    //     blockChain.PendingEvidences.Add(testEvidence.Id, testEvidence);

    //     // Then
    //     Assert.Single(blockChain.PendingEvidences);
    // }

    // [Fact]
    // public void GetPendingEvidence_Throw_Test()
    // {
    //     var blockChain = _blockChain;
    //     var height = blockChain.Tip.Height;
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(height, address, DateTimeOffset.UtcNow);
    //     Assert.Throws<KeyNotFoundException>(
    //         () => _blockChain.PendingEvidences[testEvidence.Id]);
    // }

    // [Fact]
    // public void GetPendingEvidence_AfterAppendingBlock_Throw_Test()
    // {
    //     // Given
    //     var blockChain = _blockChain;
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var proposer = key;
    //     var address = new PrivateKey().Address;
    //     var expectedEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

    //     // When
    //     NextBlock(blockChain, proposer, ImmutableArray.Create<EvidenceBase>(expectedEvidence));

    //     // Then
    //     Assert.Throws<KeyNotFoundException>(
    //         () => blockChain.PendingEvidences[expectedEvidence.Id]);
    // }

    // [Fact]
    // public void GetPendingEvidence_Add_Test()
    // {
    //     // Given
    //     var blockChain = _blockChain;
    //     var address = new PrivateKey().Address;
    //     var expectedEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
    //     blockChain.PendingEvidences.Add(expectedEvidence.Id, expectedEvidence);

    //     // Then
    //     var actualEvidence = blockChain.PendingEvidences[expectedEvidence.Id];
    //     Assert.Equal(expectedEvidence, actualEvidence);
    // }

    // [Fact]
    // public void GetCommittedEvidence_Throw_Test()
    // {
    //     var blockChain = _blockChain;
    //     var height = blockChain.Tip.Height;
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(height, address, DateTimeOffset.UtcNow);
    //     Assert.Throws<KeyNotFoundException>(
    //         () => _blockChain.CommittedEvidences[testEvidence.Id]);
    // }

    // [Fact]
    // public void GetCommittedEvidence_Test()
    // {
    //     // Given
    //     var blockChain = _blockChain;
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var proposer = key;
    //     var address = new PrivateKey().Address;
    //     var expectedEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

    //     // When
    //     NextBlock(blockChain, proposer, ImmutableArray.Create<EvidenceBase>(expectedEvidence));

    //     // Then
    //     var actualEvidence = blockChain.CommittedEvidences[expectedEvidence.Id];
    //     Assert.Equal(expectedEvidence, actualEvidence);
    // }

    // [Fact]
    // public void AddEvidence_CommittedEvidence_ThrowTest()
    // {
    //     // Given
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var proposer = key;
    //     var blockChain = _blockChain;
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

    //     // When
    //     NextBlock(blockChain, proposer, [testEvidence]);

    //     // Then
    //     Assert.Throws<ArgumentException>(
    //         () => blockChain.PendingEvidences.Add(testEvidence.Id, testEvidence));
    // }

    // [Fact]
    // public void AddEvidence_PendingEvidence_ThrowTest()
    // {
    //     // Given
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var blockChain = _blockChain;
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
    //     blockChain.PendingEvidences.Add(testEvidence.Id, testEvidence);

    //     // Then
    //     Assert.Throws<ArgumentException>(
    //         () => blockChain.PendingEvidences.Add(testEvidence.Id, testEvidence));
    // }

    // [Fact]
    // public void AddEvidence_HeightGreaterThanTip_ThrowTest()
    // {
    //     // Given
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var proposer = key;
    //     var blockChain = _blockChain;
    //     var address = new PrivateKey().Address;
    //     var height = blockChain.Tip.Height + 1;
    //     var testEvidence = TestEvidence.Create(height, address, DateTimeOffset.UtcNow);

    //     // Then
    //     Assert.Throws<ArgumentException>(
    //         () => blockChain.PendingEvidences.Add(testEvidence.Id, testEvidence));
    // }

    // [Fact]
    // public void AddEvidence_ExpiredEvidence_ThrowTest()
    // {
    //     // Given
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var proposer = key;
    //     var blockChain = _blockChain;
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
    //     var index = blockChain.Tip.Height;
    //     var pendingDuration = blockChain.Options.MaxEvidencePendingDuration;
    //     var emptyEvidence = ImmutableArray<EvidenceBase>.Empty;

    //     // When
    //     for (var i = index; i < pendingDuration + 1; i++)
    //     {
    //         NextBlock(blockChain, proposer, emptyEvidence);
    //     }

    //     // Then
    //     Assert.Throws<ArgumentException>(
    //         () => blockChain.PendingEvidences.Add(testEvidence.Id, testEvidence));
    // }

    // [Fact]
    // public void AddEvidence_Test()
    // {
    //     // Given
    //     var address = RandomUtility.Address();
    //     var blockChain = _blockChain;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

    //     // When
    //     blockChain.PendingEvidences.Add(testEvidence);

    //     // Then
    //     Assert.Single(blockChain.PendingEvidences);
    // }

    // [Fact]
    // public void CommitEvidence_AddingCommittedEvidence_ThrowTest()
    // {
    //     // Given
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var proposer = key;
    //     var blockChain = _blockChain;
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

    //     // When
    //     NextBlock(blockChain, proposer, ImmutableArray.Create<EvidenceBase>(testEvidence));

    //     // Then
    //     Assert.Throws<ArgumentException>(
    //         () => blockChain.CommitEvidence(testEvidence));
    // }

    // [Fact]
    // public void CommitEvidence_AddingExpiredEvidence_ThrowTest()
    // {
    //     // Given
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var proposer = key;
    //     var blockChain = _blockChain;
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
    //     var index = blockChain.Tip.Height;
    //     var pendingDuration = blockChain.Options.MaxEvidencePendingDuration;
    //     var emptyEvidence = ImmutableArray<EvidenceBase>.Empty;

    //     // When
    //     for (var i = index; i < pendingDuration + 1; i++)
    //     {
    //         NextBlock(blockChain, proposer, emptyEvidence);
    //     }

    //     // Then
    //     Assert.Throws<ArgumentException>(
    //         () => blockChain.CommitEvidence(testEvidence));
    // }

    // [Fact]
    // public void CommitEvidence_WithoutPendingEvidence_Test()
    // {
    //     // Given
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var blockChain = _blockChain;
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

    //     // When
    //     blockChain.CommitEvidence(testEvidence);

    //     // Then
    //     Assert.True(blockChain.IsEvidenceCommitted(testEvidence.Id));
    // }

    // [Fact]
    // public void CommitEvidence_Test()
    // {
    //     // Given
    //     var address = new PrivateKey().Address;
    //     var blockChain = _blockChain;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
    //     blockChain.PendingEvidences.Add(testEvidence);

    //     // When
    //     blockChain.CommitEvidence(testEvidence);

    //     // Then
    //     Assert.Empty(blockChain.PendingEvidences);
    //     Assert.True(blockChain.IsEvidenceCommitted(testEvidence.Id));
    // }

    // [Fact]
    // public void IsEvidencePending_True_Test()
    // {
    //     // Given
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
    //     _blockChain.PendingEvidences.Add(testEvidence);

    //     // Then
    //     Assert.True(_blockChain.IsEvidencePending(testEvidence.Id));
    // }

    // [Fact]
    // public void IsEvidencePending_False_Test()
    // {
    //     // Given
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

    //     // Then
    //     Assert.False(_blockChain.IsEvidencePending(testEvidence.Id));
    // }

    // [Fact]
    // public void IsEvidenceCommitted_True_Test()
    // {
    //     // Given
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
    //     _blockChain.PendingEvidences.Add(testEvidence);
    //     _blockChain.CommitEvidence(testEvidence);

    //     // Then
    //     Assert.True(_blockChain.IsEvidenceCommitted(testEvidence.Id));
    // }

    // [Fact]
    // public void IsEvidenceCommitted_False_Test()
    // {
    //     // Given
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
    //     _blockChain.AddEvidence(testEvidence);

    //     // Then
    //     Assert.False(_blockChain.IsEvidenceCommitted(testEvidence.Id));
    // }

    // [Fact]
    // public void IsEvidenceExpired_True_Test()
    // {
    //     // Given
    //     var address = new PrivateKey().Address;
    //     var height = Random.Shared.Next(1, 6);
    //     var testEvidence = TestEvidence.Create(height, address, DateTimeOffset.UtcNow);
    //     var index = _blockChain.Tip.Height;
    //     var pendingDuration = _blockChain.Options.MaxEvidencePendingDuration;
    //     var emptyEvidence = ImmutableArray<EvidenceBase>.Empty;

    //     // When
    //     for (var i = index; i < testEvidence.Height + pendingDuration + 1; i++)
    //     {
    //         NextBlock(_blockChain, TestUtils.ValidatorPrivateKeys[0], emptyEvidence);
    //     }

    //     // Then
    //     Assert.True(_blockChain.IsEvidenceExpired(testEvidence));
    // }

    // [Fact]
    // public void IsEvidenceExpired_False_Test()
    // {
    //     // Given
    //     var address = new PrivateKey().Address;
    //     var height = Random.Shared.Next(1, 6);
    //     var testEvidence = TestEvidence.Create(height, address, DateTimeOffset.UtcNow);
    //     var index = _blockChain.Tip.Height;
    //     var emptyEvidence = ImmutableArray<EvidenceBase>.Empty;

    //     // When
    //     for (var i = index; i < testEvidence.Height; i++)
    //     {
    //         NextBlock(_blockChain, TestUtils.ValidatorPrivateKeys[0], emptyEvidence);
    //     }

    //     // Then
    //     Assert.False(_blockChain.IsEvidenceExpired(testEvidence));
    // }

    // [Fact]
    // public void DeletePendingEvidence_True_Test()
    // {
    //     // Given
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);
    //     _blockChain.AddEvidence(testEvidence);

    //     // Then
    //     Assert.True(_blockChain.DeletePendingEvidence(testEvidence.Id));
    // }

    // [Fact]
    // public void DeletePendingEvidence_False_Test()
    // {
    //     // Given
    //     var address = new PrivateKey().Address;
    //     var testEvidence = TestEvidence.Create(0, address, DateTimeOffset.UtcNow);

    //     // Then
    //     Assert.False(_blockChain.DeletePendingEvidence(testEvidence.Id));
    //     Assert.False(_blockChain.IsEvidencePending(testEvidence.Id));
    //     Assert.False(_blockChain.IsEvidenceCommitted(testEvidence.Id));
    // }

    // [Fact]
    // public void AddEvidence_CommitEvidence_DuplicatedVoteEvidence_Test()
    // {
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var proposer = key;
    //     var blockChain = _blockChain;
    //     var voteRef = new VoteMetadata
    //     {
    //         Height = blockChain.Tip.Height,
    //         Round = 2,
    //         BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = key.PublicKey,
    //         ValidatorPower = BigInteger.One,
    //         Flag = VoteType.PreCommit,
    //     }.Sign(key);
    //     var voteDup = new VoteMetadata
    //     {
    //         Height = blockChain.Tip.Height,
    //         Round = 2,
    //         BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = key.PublicKey,
    //         ValidatorPower = BigInteger.One,
    //         Flag = VoteType.PreCommit,
    //     }.Sign(key);
    //     var evidence = DuplicateVoteEvidence.Create(voteRef, voteDup, TestUtils.Validators);

    //     Assert.Empty(blockChain.PendingEvidences);
    //     Assert.False(blockChain.IsEvidencePending(evidence.Id));
    //     Assert.False(blockChain.IsEvidenceCommitted(evidence.Id));

    //     blockChain.AddEvidence(evidence);
    //     NextBlock(blockChain, proposer, ImmutableArray<EvidenceBase>.Empty);

    //     Assert.Single(blockChain.PendingEvidences);
    //     Assert.Equal(evidence, blockChain.PendingEvidences.First());
    //     Assert.True(blockChain.IsEvidencePending(evidence.Id));
    //     Assert.False(blockChain.IsEvidenceCommitted(evidence.Id));

    //     blockChain.CommitEvidence(evidence);

    //     Assert.Empty(blockChain.PendingEvidences);
    //     Assert.False(blockChain.IsEvidencePending(evidence.Id));
    //     Assert.True(blockChain.IsEvidenceCommitted(evidence.Id));
    // }

    // [Fact]
    // public void CommitEvidence_DuplicateVoteEvidence_Test()
    // {
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var blockChain = _blockChain;
    //     var voteRef = new VoteMetadata
    //     {
    //         Height = blockChain.Tip.Height,
    //         Round = 2,
    //         BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = key.PublicKey,
    //         ValidatorPower = BigInteger.One,
    //         Flag = VoteType.PreCommit,
    //     }.Sign(key);
    //     var voteDup = new VoteMetadata
    //     {
    //         Height = blockChain.Tip.Height,
    //         Round = 2,
    //         BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = key.PublicKey,
    //         ValidatorPower = BigInteger.One,
    //         Flag = VoteType.PreCommit,
    //     }.Sign(key);
    //     var evidence = DuplicateVoteEvidence.Create(voteRef, voteDup, TestUtils.Validators);

    //     Assert.Empty(blockChain.PendingEvidences);
    //     Assert.False(blockChain.PendingEvidences.ContainsKey(evidence.Id));
    //     Assert.False(blockChain.CommittedEvidences.ContainsKey(evidence.Id));

    //     blockChain.CommitEvidence(evidence);

    //     Assert.Empty(blockChain.PendingEvidences);
    //     Assert.False(blockChain.IsEvidencePending(evidence.Id));
    //     Assert.True(blockChain.IsEvidenceCommitted(evidence.Id));
    // }

    // [Fact]
    // public void AddEvidence_DuplicateVoteEvidence_FromNonValidator_ThrowTest()
    // {
    //     var key = new PrivateKey();
    //     var voteRef = new VoteMetadata
    //     {
    //         Height = _blockChain.Tip.Height,
    //         Round = 2,
    //         BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = key.PublicKey,
    //         ValidatorPower = BigInteger.One,
    //         Flag = VoteType.PreCommit,
    //     }.Sign(key);
    //     var voteDup = new VoteMetadata
    //     {
    //         Height = _blockChain.Tip.Height,
    //         Round = 2,
    //         BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = key.PublicKey,
    //         ValidatorPower = BigInteger.One,
    //         Flag = VoteType.PreCommit,
    //     }.Sign(key);
    //     var validators = ImmutableSortedSet.Create(Validator.Create(key.PublicKey, BigInteger.One));
    //     var evidence = DuplicateVoteEvidence.Create(voteRef, voteDup, validators);

    //     Assert.Empty(_blockChain.PendingEvidences);
    //     Assert.False(_blockChain.IsEvidencePending(evidence.Id));
    //     Assert.False(_blockChain.IsEvidenceCommitted(evidence.Id));

    //     Assert.Throws<ValidationException>(() => _blockChain.AddEvidence(evidence));

    //     Assert.Empty(_blockChain.PendingEvidences);
    //     Assert.False(_blockChain.IsEvidencePending(evidence.Id));
    //     Assert.False(_blockChain.IsEvidenceCommitted(evidence.Id));
    // }

    // [Fact]
    // public void EvidenceExpired_ThrowTest()
    // {
    //     var key = TestUtils.ValidatorPrivateKeys[0];
    //     var proposer = key;
    //     var blockChain = _blockChain;
    //     var voteRef = new VoteMetadata
    //     {
    //         Height = blockChain.Tip.Height,
    //         Round = 2,
    //         BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = key.PublicKey,
    //         ValidatorPower = BigInteger.One,
    //         Flag = VoteType.PreCommit,
    //     }.Sign(key);
    //     var voteDup = new VoteMetadata
    //     {
    //         Height = blockChain.Tip.Height,
    //         Round = 2,
    //         BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = key.PublicKey,
    //         ValidatorPower = BigInteger.One,
    //         Flag = VoteType.PreCommit,
    //     }.Sign(key);
    //     var evidence = DuplicateVoteEvidence.Create(voteRef, voteDup, TestUtils.Validators);

    //     Assert.Empty(blockChain.PendingEvidences);
    //     Assert.False(blockChain.IsEvidencePending(evidence.Id));
    //     Assert.False(blockChain.IsEvidenceCommitted(evidence.Id));

    //     blockChain.AddEvidence(evidence);

    //     Assert.Single(blockChain.PendingEvidences);
    //     Assert.Equal(evidence, blockChain.PendingEvidences.First());
    //     Assert.True(blockChain.IsEvidencePending(evidence.Id));
    //     Assert.False(blockChain.IsEvidenceCommitted(evidence.Id));

    //     var pendingDuration = blockChain.Options.MaxEvidencePendingDuration;
    //     var emptyEvidence = ImmutableArray<EvidenceBase>.Empty;

    //     for (var i = 0; i < pendingDuration; i++)
    //     {
    //         NextBlock(blockChain, proposer, emptyEvidence);
    //     }

    //     Assert.Throws<InvalidOperationException>(
    //         () => NextBlock(blockChain, proposer, blockChain.PendingEvidences));

    //     Assert.Single(blockChain.PendingEvidences);

    //     NextBlock(blockChain, proposer, emptyEvidence);

    //     Assert.Empty(blockChain.PendingEvidences);
    // }

    // private static Block NextBlock(
    //     BlockChain blockChain, PrivateKey proposer, ImmutableArray<EvidenceBase> evidence)
    // {
    //     var tip = blockChain.Tip;
    //     var block = blockChain.ProposeBlock(
    //         proposer: proposer,
    //         lastCommit: TestUtils.CreateBlockCommit(tip, true),
    //         evidences: [.. evidence]);
    //     blockChain.Append(block, TestUtils.CreateBlockCommit(block, true));
    //     return block;
    // }
}
