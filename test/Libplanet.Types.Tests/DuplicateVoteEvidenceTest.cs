using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Types.Tests;

public sealed class DuplicateVoteEvidenceTest(ITestOutputHelper output)
{
    [Fact]
    public void Create_WithDifferentHeight_FailTest()
    {
        // Given
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Signer(random);
        var validatorAddress = signer.Address;
        ImmutableSortedSet<Validator> validators =
        [
            new Validator { Address = validatorAddress },
        ];

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);
        var voteDup = new VoteMetadata
        {
            Height = 2,
            Round = 2,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);

        // When, Then
        ModelAssert.Throws(
            DuplicateVoteEvidence.Create(voteRef, voteDup, validators),
            nameof(DuplicateVoteEvidence.VoteDup));
    }

    [Fact]
    public void Create_WithDifferentRound_FailTest()
    {
        // Given
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Signer(random);
        var validatorAddress = signer.Address;
        ImmutableSortedSet<Validator> validators =
        [
            new Validator { Address = validatorAddress },
        ];

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 3,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);

        // When, Then
        ModelAssert.Throws(
            DuplicateVoteEvidence.Create(voteRef, voteDup, validators),
            nameof(DuplicateVoteEvidence.VoteDup));
    }

    [Fact]
    public void Create_WithDifferentPublicKey_FailTest()
    {
        // Given
        var random = RandomUtility.GetRandom(output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 2);
        var validatorAddresses = signers.Select(item => item.Address).ToArray();
        var validators = validatorAddresses.Select(item => new Validator { Address = item })
            .ToImmutableSortedSet();

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddresses[0],
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signers[0]);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddresses[1],
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signers[1]);

        // When, Then
        ModelAssert.Throws(
            DuplicateVoteEvidence.Create(voteRef, voteDup, validators),
            nameof(DuplicateVoteEvidence.VoteDup));
    }

    [Fact]
    public void Create_WithDifferentFlag_FailTest()
    {
        // Given
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Signer(random);
        var validatorAddress = signer.Address;
        ImmutableSortedSet<Validator> validators =
        [
            new Validator { Address = validatorAddress },
        ];

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreVote,
        }.Sign(signer);

        // When, Then
        ModelAssert.Throws(
            DuplicateVoteEvidence.Create(voteRef, voteDup, validators),
            nameof(DuplicateVoteEvidence.VoteDup));
    }

    [Fact]
    public void Create_WithSameBlock_FailTest()
    {
        // Given
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Signer(random);
        var validatorAddress = signer.Address;
        var blockHash = RandomUtility.BlockHash(random);
        ImmutableSortedSet<Validator> validators =
        [
            new Validator { Address = validatorAddress },
        ];

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);

        // When, Then
        ModelAssert.Throws(
            DuplicateVoteEvidence.Create(voteRef, voteDup, validators),
            nameof(DuplicateVoteEvidence.VoteDup));
    }

    [Fact]
    public void Serialize_and_Deserialize_Test()
    {
        // Given
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Signer(random);
        var validatorAddress = signer.Address;
        ImmutableSortedSet<Validator> validators =
        [
            new Validator { Address = validatorAddress },
        ];

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer);

        // When
        var expectedEvidence = DuplicateVoteEvidence.Create(voteRef, voteDup, validators);

        // Then
        var bencoded = ModelSerializer.SerializeToBytes(expectedEvidence);
        var actualEvidence = ModelSerializer.DeserializeFromBytes<DuplicateVoteEvidence>(bencoded);

        Assert.Equal(expectedEvidence, actualEvidence);
    }
}
