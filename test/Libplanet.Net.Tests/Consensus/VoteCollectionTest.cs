using Libplanet.Net.Consensus;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus;

public sealed class VoteCollectionTest(ITestOutputHelper output)
{
    [Fact]
    public void Majority()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var votes = new VoteCollection(0, 0, VoteType.PreCommit, TestUtils.Validators);
        Assert.False(votes.HasOneThirdsAny);
        Assert.False(votes.HasTwoThirdsAny);
        Assert.False(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajorityBlockHash(out var hash0));
        Assert.Equal(default, hash0);

        votes.Add(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.PrivateKeys[0]));
        Assert.False(votes.HasOneThirdsAny);
        Assert.False(votes.HasTwoThirdsAny);
        Assert.False(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajorityBlockHash(out var hash1));
        Assert.Equal(default, hash1);

        votes.Add(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[1].Address,
            ValidatorPower = TestUtils.Validators[1].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.PrivateKeys[1]));
        Assert.True(votes.HasOneThirdsAny);
        Assert.False(votes.HasTwoThirdsAny);
        Assert.False(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajorityBlockHash(out var hash2));
        Assert.Equal(default, hash2);

        votes.Add(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[2].Address,
            ValidatorPower = TestUtils.Validators[2].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.PrivateKeys[2]));
        Assert.True(votes.HasOneThirdsAny);
        Assert.True(votes.HasTwoThirdsAny);
        Assert.False(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajorityBlockHash(out var hash3));
        Assert.Equal(default, hash3);

        votes.Add(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[3].Address,
            ValidatorPower = TestUtils.Validators[3].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.PrivateKeys[3]));
        Assert.True(votes.HasOneThirdsAny);
        Assert.True(votes.HasTwoThirdsAny);
        Assert.True(votes.HasTwoThirdsMajority);
        Assert.True(votes.TryGetMajorityBlockHash(out var hash4));
        Assert.Equal(blockHash, hash4);
    }
}
