using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Libplanet.Types.Tests;

namespace Libplanet.Tests.Blocks;

public sealed class BlockCommitTest
{
    [Fact]
    public void ToHash()
    {
        var randomHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        var keys = Enumerable.Range(0, 4).Select(_ => new PrivateKey()).ToList();
        var votes = keys.Select((key, index) =>
                new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = randomHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = key.Address,
                    ValidatorPower = index == 0 ? BigInteger.Zero : BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(key))
            .ToImmutableArray();
        var blockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = randomHash,
            Votes = votes,
        };

        var commitHash = blockCommit.ToHash();
        var expected = HashDigest<SHA256>.HashData(ModelSerializer.SerializeToBytes(blockCommit));

        Assert.Equal(commitHash, expected);
    }

    [Fact]
    public void HeightAndRoundMustNotBeNegative()
    {
        var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        var key = new PrivateKey();
        var votes = ImmutableArray.Create(
            new VoteMetadata
            {
                Height = 0,
                Round = 0,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = key.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(key));

        // Negative height is not allowed.
        var exception1 = ValidationUtility.Throws(
            new BlockCommit
            {
                Height = -1,
                Round = 0,
                BlockHash = hash,
                Votes = votes,
            });
        Assert.Contains(nameof(BlockCommit.Height), exception1.ValidationResult.MemberNames);

        // Negative round is not allowed.
        var exception2 = ValidationUtility.Throws(
            new BlockCommit
            {
                Height = 0,
                Round = -1,
                BlockHash = hash,
                Votes = votes,
            });
        Assert.Contains(nameof(BlockCommit.Round), exception2.ValidationResult.MemberNames);
    }

    [Fact]
    public void VotesCannotBeEmpty()
    {
        var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        var exception1 = ValidationUtility.Throws(
            new BlockCommit
            {
                Height = 0,
                Round = 0,
                BlockHash = hash,
                Votes = default,
            });
        Assert.Contains(nameof(BlockCommit.Votes), exception1.ValidationResult.MemberNames);

        var exception2 = ValidationUtility.Throws(
            new BlockCommit
            {
                Height = 0,
                Round = 0,
                BlockHash = hash,
                Votes = [],
            });
        Assert.Contains(nameof(BlockCommit.Votes), exception2.ValidationResult.MemberNames);
    }

    [Fact]
    public void EveryVoteMustHaveSameHeightAndRoundAsBlockCommit()
    {
        var height = 2;
        var round = 3;
        var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        var key = new PrivateKey();

        // Vote with different height is not allowed.
        var votes1 = ImmutableArray.Create(
            new VoteMetadata
            {
                Height = height + 1,
                Round = round,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = key.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(key));

        var exception1 = ValidationUtility.Throws(
            new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Votes = votes1,
            });
        Assert.Contains(nameof(BlockCommit.Votes), exception1.ValidationResult.MemberNames);

        // Vote with different round is not allowed.
        var votes2 = ImmutableArray.Create(
            new VoteMetadata
            {
                Height = height,
                Round = round + 1,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = key.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(key));

        var exception2 = ValidationUtility.Throws(
            new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Votes = votes2,
            });
        Assert.Contains(nameof(BlockCommit.Votes), exception2.ValidationResult.MemberNames);
    }

    [Fact]
    public void EveryVoteMustHaveSameHashAsBlockCommit()
    {
        var height = 2;
        var round = 3;
        var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        var badHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        var key = new PrivateKey();

        var votes = ImmutableArray.Create(
            new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = badHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = key.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(key));

        var exception1 = ValidationUtility.Throws(
            new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Votes = votes,
            });
        Assert.Contains(nameof(BlockCommit.Votes), exception1.ValidationResult.MemberNames);
    }

    [Fact]
    public void EveryVoteFlagMustBeNullOrPreCommit()
    {
        var height = 2;
        var round = 3;
        var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        var keys = Enumerable.Range(0, 4).Select(_ => new PrivateKey()).ToList();
        var preCommitVotes = keys.Select(
                key => new VoteMetadata
                {
                    Height = height,
                    Round = round,
                    BlockHash = hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = key.Address,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(key))
            .ToImmutableArray();

        var votes = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = keys[0].Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.Null,
            }.Sign(null))
            .AddRange(preCommitVotes.Skip(1));
        _ = new BlockCommit
        {
            Height = height,
            Round = round,
            BlockHash = hash,
            Votes = votes,
        };

        votes = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = keys[0].Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.Unknown,
            }.Sign(null))
            .AddRange(preCommitVotes.Skip(1));
        Assert.Throws<ArgumentException>(() => new BlockCommit
        {
            Height = height,
            Round = round,
            BlockHash = hash,
            Votes = votes,
        });

        votes = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = keys[0].Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreVote,
            }.Sign(keys[0]))
            .AddRange(preCommitVotes.Skip(1));
        Assert.Throws<ArgumentException>(() => new BlockCommit
        {
            Height = height,
            Round = round,
            BlockHash = hash,
            Votes = votes,
        });

        votes = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = keys[0].Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(keys[0]))
            .AddRange(preCommitVotes.Skip(1));
        _ = new BlockCommit
        {
            Height = height,
            Round = round,
            BlockHash = hash,
            Votes = votes,
        };
    }
}
