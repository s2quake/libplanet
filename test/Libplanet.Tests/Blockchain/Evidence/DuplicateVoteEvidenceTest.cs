using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Tests;

namespace Libplanet.Tests.Blockchain.Evidence;

public class DuplicateVoteEvidenceTest
{
    [Fact]
    public void Create_WithDifferentHeight_FailTest()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorAddress = privateKey.Address;
        var validators = ImmutableSortedSet.Create(
        [
            new Validator { Address = validatorAddress },
        ]);

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 2,
            Round = 2,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);

        // When, Then
        ValidationUtility.Throws(
            DuplicateVoteEvidence.Create(voteRef, voteDup, validators),
            nameof(DuplicateVoteEvidence.VoteDup));
    }

    [Fact]
    public void Create_WithDifferentRound_FailTest()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorAddress = privateKey.Address;
        var validatorList = new List<Validator>
        {
            new Validator { Address = validatorAddress },
        };

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 3,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);

        // When, Then
        ValidationUtility.Throws(
            DuplicateVoteEvidence.Create(voteRef, voteDup, [.. validatorList]),
            nameof(DuplicateVoteEvidence.VoteDup));
    }

    [Fact]
    public void Create_WithDifferentPublicKey_FailTest()
    {
        // Given
        var privateKeys = new PrivateKey[]
        {
            new PrivateKey(),
            new PrivateKey(),
        };
        var validatorAddresses = privateKeys.Select(item => item.Address).ToArray();
        var validators = validatorAddresses.Select(item => new Validator { Address = item })
            .ToImmutableSortedSet();

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddresses[0],
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKeys[0]);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddresses[1],
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKeys[1]);

        // When, Then
        ValidationUtility.Throws(
            DuplicateVoteEvidence.Create(voteRef, voteDup, validators),
            nameof(DuplicateVoteEvidence.VoteDup));
    }

    [Fact]
    public void Create_WithDifferentFlag_FailTest()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorAddress = privateKey.Address;
        var validators = ImmutableSortedSet.Create(
        [
            new Validator { Address = validatorAddress },
        ]);

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreVote,
        }.Sign(privateKey);

        // When, Then
        ValidationUtility.Throws(
            DuplicateVoteEvidence.Create(voteRef, voteDup, validators),
            nameof(DuplicateVoteEvidence.VoteDup));
    }

    [Fact]
    public void Create_WithSameBlock_FailTest()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorAddress = privateKey.Address;
        var blockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        var validatorList = new List<Validator>
        {
            new Validator { Address = validatorAddress },
        };

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);

        // When, Then
        ValidationUtility.Throws(
            DuplicateVoteEvidence.Create(voteRef, voteDup, [.. validatorList]),
            nameof(DuplicateVoteEvidence.VoteDup));
    }

    [Fact]
    public void Serialize_and_Deserialize_Test()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorAddress = privateKey.Address;
        var validators = ImmutableSortedSet.Create(
        [
            new Validator { Address = validatorAddress },
        ]);

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validatorAddress,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);

        // When
        var expectedEvidence = DuplicateVoteEvidence.Create(voteRef, voteDup, validators);

        // Then
        var bencoded = ModelSerializer.SerializeToBytes(expectedEvidence);
        var actualEvidence = ModelSerializer.DeserializeFromBytes<DuplicateVoteEvidence>(bencoded);

        Assert.Equal(expectedEvidence, actualEvidence);
    }
}
