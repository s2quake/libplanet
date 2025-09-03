using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.TestUtilities;

namespace Libplanet.Types.Tests;

public sealed class BlockCommitTest(ITestOutputHelper output)
{
    [Fact]
    public void ToHash()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 4);
        var votes = signers.Select((signer, index) =>
                new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = blockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = signer.Address,
                    ValidatorPower = RandomUtility.PositiveBigInteger(random),
                    Type = VoteType.PreCommit,
                }.Sign(signer))
            .ToImmutableArray();
        var blockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = blockHash,
            Votes = votes,
        };

        var commitHash = blockCommit.ToHash();
        var expected = HashDigest<SHA256>.HashData(ModelSerializer.SerializeToBytes(blockCommit));

        Assert.Equal(commitHash, expected);
    }

    [Fact]
    public void HeightAndRoundMustNotBeNegative()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var signer = RandomUtility.Signer(random);
        var votes = ImmutableArray.Create(
            new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = blockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = RandomUtility.PositiveBigInteger(random),
                Type = VoteType.PreCommit,
            }.Sign(signer));

        // Non positive height is not allowed.
        var exception1 = ModelAssert.Throws(
            new BlockCommit
            {
                Height = 0,
                Round = 0,
                BlockHash = blockHash,
                Votes = votes,
            });
        Assert.Contains(nameof(BlockCommit.Height), exception1.ValidationResult.MemberNames);

        // Negative round is not allowed.
        var exception2 = ModelAssert.Throws(
            new BlockCommit
            {
                Height = 1,
                Round = -1,
                BlockHash = blockHash,
                Votes = votes,
            });
        Assert.Contains(nameof(BlockCommit.Round), exception2.ValidationResult.MemberNames);
    }

    [Fact]
    public void VotesCannotBeEmpty()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var exception1 = ModelAssert.Throws(
            new BlockCommit
            {
                Height = 1,
                Round = 0,
                BlockHash = blockHash,
                Votes = default,
            });
        Assert.Contains(nameof(BlockCommit.Votes), exception1.ValidationResult.MemberNames);

        var exception2 = ModelAssert.Throws(
            new BlockCommit
            {
                Height = 1,
                Round = 0,
                BlockHash = blockHash,
                Votes = [],
            });
        Assert.Contains(nameof(BlockCommit.Votes), exception2.ValidationResult.MemberNames);
    }

    [Fact]
    public void EveryVoteMustHaveSameHeightAndRoundAsBlockCommit()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var signer = RandomUtility.Signer(random);
        var height = 2;
        var round = 3;

        // Vote with different height is not allowed.
        var votes1 = ImmutableArray.Create(
            new VoteMetadata
            {
                Height = height + 1,
                Round = round,
                BlockHash = blockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Type = VoteType.PreCommit,
            }.Sign(signer));

        var exception1 = ModelAssert.Throws(
            new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = blockHash,
                Votes = votes1,
            });
        Assert.Contains(nameof(BlockCommit.Votes), exception1.ValidationResult.MemberNames);

        // Vote with different round is not allowed.
        var votes2 = ImmutableArray.Create(
            new VoteMetadata
            {
                Height = height,
                Round = round + 1,
                BlockHash = blockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Type = VoteType.PreCommit,
            }.Sign(signer));

        var exception2 = ModelAssert.Throws(
            new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = blockHash,
                Votes = votes2,
            });
        Assert.Contains(nameof(BlockCommit.Votes), exception2.ValidationResult.MemberNames);
    }

    [Fact]
    public void EveryVoteMustHaveSameHashAsBlockCommit()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var invalidHash = RandomUtility.BlockHash(random);
        var signer = RandomUtility.Signer(random);
        var height = 2;
        var round = 3;

        var votes = ImmutableArray.Create(
            new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = invalidHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Type = VoteType.PreCommit,
            }.Sign(signer));

        var exception1 = ModelAssert.Throws(
            new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = blockHash,
                Votes = votes,
            });
        Assert.Contains(nameof(BlockCommit.Votes), exception1.ValidationResult.MemberNames);
    }

    [Fact]
    public void EveryVoteTypeMustBeNullOrPreCommit()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 4);
        var height = 2;
        var round = 3;
        var preCommitVotes = signers.Select(
                key => new VoteMetadata
                {
                    Height = height,
                    Round = round,
                    BlockHash = blockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = key.Address,
                    ValidatorPower = BigInteger.One,
                    Type = VoteType.PreCommit,
                }.Sign(key))
            .ToImmutableArray();

        var votes = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = blockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signers[0].Address,
                ValidatorPower = BigInteger.One,
                Type = VoteType.Null,
            }.WithoutSignature())
            .AddRange(preCommitVotes.Skip(1));
        _ = new BlockCommit
        {
            Height = height,
            Round = round,
            BlockHash = blockHash,
            Votes = votes,
        };

        votes =
        [
            new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = blockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signers[0].Address,
                ValidatorPower = BigInteger.One,
                Type = VoteType.Unknown,
            }.WithoutSignature(),
            .. preCommitVotes.Skip(1),
        ];
        ModelAssert.Throws(
            new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = blockHash,
                Votes = votes,
            },
            nameof(BlockCommit.Votes));

        votes =
        [
            new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = blockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signers[0].Address,
                ValidatorPower = BigInteger.One,
                Type = VoteType.PreVote,
            }.Sign(signers[0]),
            .. preCommitVotes.Skip(1),
        ];
        ModelAssert.Throws(
            new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = blockHash,
                Votes = votes,
            },
            nameof(BlockCommit.Votes));

        votes =
        [
            new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = blockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signers[0].Address,
                ValidatorPower = BigInteger.One,
                Type = VoteType.PreCommit,
            }.Sign(signers[0]),
            .. preCommitVotes.Skip(1),
        ];
        _ = new BlockCommit
        {
            Height = height,
            Round = round,
            BlockHash = blockHash,
            Votes = votes,
        };
    }
}
