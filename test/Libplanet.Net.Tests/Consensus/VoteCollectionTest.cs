using Libplanet.Net.Consensus;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus;

public sealed class VoteCollectionTest(ITestOutputHelper output)
{
    [Fact]
    public void BaseTest()
    {
        var votes = new VoteCollection(1, 0, VoteType.PreCommit, TestUtils.Validators);

        Assert.Equal(1, votes.Height);
        Assert.Equal(0, votes.Round);
        Assert.Equal(VoteType.PreCommit, votes.Type);
        Assert.Equal(TestUtils.Validators, votes.Validators);
    }

    [Fact]
    public void Add()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var votes = new VoteCollection(1, 0, VoteType.PreCommit, TestUtils.Validators);
        var vote = new VoteMetadata
        {
            Validator = TestUtils.Validators[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Height = 1,
            Round = 0,
            Type = VoteType.PreCommit,
            BlockHash = blockHash,
        }.Sign(TestUtils.PrivateKeys[0]);
        votes.Add(vote);
        Assert.Equal(1, votes.Count);
        Assert.False(votes.HasOneThirdsAny);
        Assert.False(votes.HasTwoThirdsAny);
        Assert.False(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajority23(out var _));
    }

    [Fact]
    public void Add_2Votes()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var votes = new VoteCollection(1, 0, VoteType.PreCommit, TestUtils.Validators);
        for (var i = 0; i < 2; i++)
        {
            var vote = new VoteMetadata
            {
                Validator = TestUtils.Validators[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Height = 1,
                Round = 0,
                Type = VoteType.PreCommit,
                BlockHash = blockHash,
            }.Sign(TestUtils.PrivateKeys[i]);
            votes.Add(vote);
        }

        Assert.Equal(2, votes.Count);
        Assert.True(votes.HasOneThirdsAny);
        Assert.False(votes.HasTwoThirdsAny);
        Assert.False(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajority23(out var _));
    }

    [Fact]
    public void Add_3Votes()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var votes = new VoteCollection(1, 0, VoteType.PreCommit, TestUtils.Validators);
        for (var i = 0; i < 3; i++)
        {
            var vote = new VoteMetadata
            {
                Validator = TestUtils.Validators[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Height = 1,
                Round = 0,
                Type = VoteType.PreCommit,
                BlockHash = blockHash,
            }.Sign(TestUtils.PrivateKeys[i]);
            votes.Add(vote);
        }

        Assert.Equal(3, votes.Count);
        Assert.True(votes.HasOneThirdsAny);
        Assert.True(votes.HasTwoThirdsAny);
        Assert.True(votes.HasTwoThirdsMajority);
        Assert.True(votes.TryGetMajority23(out var majorityBlockHash));
        Assert.Equal(blockHash, majorityBlockHash);
    }

    [Fact]
    public void Remove()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var votes = new VoteCollection(1, 0, VoteType.PreCommit, TestUtils.Validators);
        var vote = new VoteMetadata
        {
            Validator = TestUtils.Validators[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Height = 1,
            Round = 0,
            Type = VoteType.PreCommit,
            BlockHash = blockHash,
        }.Sign(TestUtils.PrivateKeys[0]);
        votes.Add(vote);

        votes.Remove(vote.Validator);
        Assert.Equal(0, votes.Count);
    }

    [Fact]
    public void Remove_After_Adding_2_Votes()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var votes = new VoteCollection(1, 0, VoteType.PreCommit, TestUtils.Validators);
        for (var i = 0; i < 2; i++)
        {
            var vote = new VoteMetadata
            {
                Validator = TestUtils.Validators[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Height = 1,
                Round = 0,
                Type = VoteType.PreCommit,
                BlockHash = blockHash,
            }.Sign(TestUtils.PrivateKeys[i]);
            votes.Add(vote);
        }

        votes.Remove(TestUtils.Validators[0].Address);
        Assert.Equal(1, votes.Count);
        Assert.False(votes.HasOneThirdsAny);
    }

    [Fact]
    public void Remove_After_Adding_3_Votes()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var votes = new VoteCollection(1, 0, VoteType.PreCommit, TestUtils.Validators);
        for (var i = 0; i < 3; i++)
        {
            var vote = new VoteMetadata
            {
                Validator = TestUtils.Validators[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Height = 1,
                Round = 0,
                Type = VoteType.PreCommit,
                BlockHash = blockHash,
            }.Sign(TestUtils.PrivateKeys[i]);
            votes.Add(vote);
        }

        votes.Remove(TestUtils.Validators[0].Address);
        Assert.Equal(2, votes.Count);
        Assert.True(votes.HasOneThirdsAny);
        Assert.False(votes.HasTwoThirdsAny);
        Assert.False(votes.HasTwoThirdsMajority);
        Assert.False(votes.TryGetMajority23(out var _));
    }

    [Fact]
    public void Remove_After_Adding_4_Votes()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash = RandomUtility.BlockHash(random);
        var votes = new VoteCollection(1, 0, VoteType.PreCommit, TestUtils.Validators);
        for (var i = 0; i < 4; i++)
        {
            var vote = new VoteMetadata
            {
                Validator = TestUtils.Validators[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Height = 1,
                Round = 0,
                Type = VoteType.PreCommit,
                BlockHash = blockHash,
            }.Sign(TestUtils.PrivateKeys[i]);
            votes.Add(vote);
        }

        votes.Remove(TestUtils.Validators[0].Address);
        Assert.Equal(3, votes.Count);
        Assert.True(votes.HasOneThirdsAny);
        Assert.True(votes.HasTwoThirdsAny);
        Assert.True(votes.HasTwoThirdsMajority);
        Assert.True(votes.TryGetMajority23(out var majorityBlockHash));
        Assert.Equal(blockHash, majorityBlockHash);
    }
}
