using Libplanet.Net.Consensus;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;

namespace Libplanet.Net.Tests.Consensus;

public sealed class VoteCollectionClassicTest(ITestOutputHelper output)
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
        Assert.False(votes.TryGetMajority23(out var decidedBlockHash1));
        Assert.Equal(default, decidedBlockHash1);

        votes.Add(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.Signers[0]));
        Assert.False(votes.HasOneThirdsAny);
        Assert.False(votes.HasTwoThirdsAny);
        Assert.False(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajority23(out var decidedBlockHash2));
        Assert.Equal(default, decidedBlockHash2);

        votes.Add(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[1].Address,
            ValidatorPower = TestUtils.Validators[1].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.Signers[1]));
        Assert.True(votes.HasOneThirdsAny);
        Assert.False(votes.HasTwoThirdsAny);
        Assert.False(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajority23(out var decidedBlockHash3));
        Assert.Equal(default, decidedBlockHash3);

        votes.Add(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[2].Address,
            ValidatorPower = TestUtils.Validators[2].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.Signers[2]));
        Assert.True(votes.HasOneThirdsAny);
        Assert.True(votes.HasTwoThirdsAny);
        Assert.False(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajority23(out var decidedBlockHash4));
        Assert.Equal(default, decidedBlockHash4);

        votes.Add(new VoteMetadata
        {
            Height = 0,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[3].Address,
            ValidatorPower = TestUtils.Validators[3].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.Signers[3]));
        Assert.True(votes.HasOneThirdsAny);
        Assert.True(votes.HasTwoThirdsAny);
        Assert.True(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajority23(out var decidedBlockHash5));
        Assert.Equal(blockHash, decidedBlockHash5);
    }
}
