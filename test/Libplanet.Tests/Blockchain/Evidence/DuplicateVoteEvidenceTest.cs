using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Evidence;
using Xunit;

namespace Libplanet.Tests.Blockchain.Evidence;

public class DuplicateVoteEvidenceTest
{
    [Fact]
    public void Create_WithDifferentHeight_FailTest()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorPublicKey = privateKey.PublicKey;
        var validators = ImmutableSortedSet.Create(
        [
            new Validator(validatorPublicKey, BigInteger.One),
        ]);

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 2,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);

        // When, Then
        Assert.Throws<ArgumentException>(() =>
        {
            DuplicateVoteEvidence.Create(
                voteRef,
                voteDup,
                validators,
                DateTimeOffset.UtcNow);
        });
    }

    [Fact]
    public void Create_WithDifferentRound_FailTest()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorPublicKey = privateKey.PublicKey;
        var validatorList = new List<Validator>
        {
            new Validator(validatorPublicKey, BigInteger.One),
        };

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 3,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);

        // When, Then
        Assert.Throws<ArgumentException>(() =>
        {
            DuplicateVoteEvidence.Create(
                voteRef,
                voteDup,
                [.. validatorList],
                DateTimeOffset.UtcNow);
        });
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
        var validatorPublicKeys = privateKeys.Select(item => item.PublicKey).ToArray();
        var validators = validatorPublicKeys.Select(item => new Validator(item, BigInteger.One))
            .ToImmutableSortedSet();

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKeys[0],
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKeys[0]);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKeys[1],
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKeys[1]);

        // When, Then
        Assert.Throws<ArgumentException>(() =>
        {
            DuplicateVoteEvidence.Create(
                voteRef: voteRef,
                voteDup: voteDup,
                validators: validators,
                timestamp: DateTimeOffset.UtcNow);
        });
    }

    [Fact]
    public void Create_WithDifferentFlag_FailTest()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorPublicKey = privateKey.PublicKey;
        var validators = ImmutableSortedSet.Create(
        [
            new Validator(validatorPublicKey, BigInteger.One),
        ]);

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreVote,
        }.Sign(privateKey);

        // When, Then
        Assert.Throws<ArgumentException>(() =>
        {
            DuplicateVoteEvidence.Create(
                voteRef: voteRef,
                voteDup: voteDup,
                validators: validators,
                timestamp: DateTimeOffset.UtcNow);
        });
    }

    [Fact]
    public void Create_WithSameBlock_FailTest()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorPublicKey = privateKey.PublicKey;
        var blockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
        var validatorList = new List<Validator>
        {
            new Validator(validatorPublicKey, BigInteger.One),
        };

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);

        // When, Then
        Assert.Throws<ArgumentException>(() =>
        {
            DuplicateVoteEvidence.Create(
                voteRef: voteRef,
                voteDup: voteDup,
                validators: [.. validatorList],
                timestamp: DateTimeOffset.UtcNow);
        });
    }

    [Fact]
    public void Bencoded()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorPublicKey = privateKey.PublicKey;
        var validatorList = new List<Validator>
        {
            new Validator(validatorPublicKey, BigInteger.One),
        };

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);

        // When
        var expectedEvidence = DuplicateVoteEvidence.Create(
            voteRef: voteRef,
            voteDup: voteDup,
            validators: [.. validatorList],
            timestamp: voteDup.Timestamp);

        // Then
        var actualEvidence = ModelSerializer.Deserialize<DuplicateVoteEvidence>(
            ModelSerializer.Serialize(expectedEvidence));

        Assert.Equal(expectedEvidence, actualEvidence);
    }

    [Fact]
    public void Serialize_and_Deserialize_Test()
    {
        // Given
        var privateKey = new PrivateKey();
        var validatorPublicKey = privateKey.PublicKey;
        var validatorList = new List<Validator>
        {
            new Validator(validatorPublicKey, BigInteger.One),
        };

        var voteRef = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);
        var voteDup = new VoteMetadata
        {
            Height = 1,
            Round = 2,
            BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = validatorPublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);

        // When
        var expectedEvidence = DuplicateVoteEvidence.Create(
            voteRef: voteRef,
            voteDup: voteDup,
            validators: [.. validatorList],
            timestamp: voteDup.Timestamp);

        // Then
        var bencoded = ModelSerializer.Serialize(expectedEvidence);
        var actualEvidence = ModelSerializer.Deserialize<DuplicateVoteEvidence>(bencoded);

        // Assert.Equal(expectedEvidence.Bencoded, actualEvidence.Bencoded);
        Assert.Equal(expectedEvidence, actualEvidence);
    }
}
