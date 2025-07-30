using Libplanet.Net.Consensus;
using Libplanet.Types;
using Libplanet.Types.Tests;

namespace Libplanet.Net.Tests.Consensus;

public class VoteSetTest
{
    [Fact]
    public void Majority()
    {
        var voteSet = new VoteSet(0, 0, VoteFlag.PreCommit, TestUtils.Validators);
        Assert.False(voteSet.HasOneThirdsAny());
        Assert.False(voteSet.HasTwoThirdsAny());
        Assert.False(voteSet.HasTwoThirdsMajority());
        Assert.False(voteSet.TwoThirdsMajority(out var hash0));
        Assert.Equal(default, hash0);

        var blockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        voteSet.AddVote(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Flag = VoteFlag.PreCommit,
        }.Sign(TestUtils.PrivateKeys[0]));
        Assert.False(voteSet.HasOneThirdsAny());
        Assert.False(voteSet.HasTwoThirdsAny());
        Assert.False(voteSet.HasTwoThirdsMajority());
        Assert.False(voteSet.TwoThirdsMajority(out var hash1));
        Assert.Equal(default, hash1);

        voteSet.AddVote(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[1].Address,
            ValidatorPower = TestUtils.Validators[1].Power,
            Flag = VoteFlag.PreCommit,
        }.Sign(TestUtils.PrivateKeys[1]));
        Assert.True(voteSet.HasOneThirdsAny());
        Assert.False(voteSet.HasTwoThirdsAny());
        Assert.False(voteSet.HasTwoThirdsMajority());
        Assert.False(voteSet.TwoThirdsMajority(out var hash2));
        Assert.Equal(default, hash2);

        voteSet.AddVote(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[2].Address,
            ValidatorPower = TestUtils.Validators[2].Power,
            Flag = VoteFlag.PreCommit,
        }.Sign(TestUtils.PrivateKeys[2]));
        Assert.True(voteSet.HasOneThirdsAny());
        Assert.True(voteSet.HasTwoThirdsAny());
        Assert.False(voteSet.HasTwoThirdsMajority());
        Assert.False(voteSet.TwoThirdsMajority(out var hash3));
        Assert.Equal(default, hash3);

        voteSet.AddVote(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[3].Address,
            ValidatorPower = TestUtils.Validators[3].Power,
            Flag = VoteFlag.PreCommit,
        }.Sign(TestUtils.PrivateKeys[3]));
        Assert.True(voteSet.HasOneThirdsAny());
        Assert.True(voteSet.HasTwoThirdsAny());
        Assert.True(voteSet.HasTwoThirdsMajority());
        Assert.True(voteSet.TwoThirdsMajority(out var hash4));
        Assert.Equal(blockHash, hash4);
    }
}
