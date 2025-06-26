using Libplanet.Net.Consensus;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus;

public sealed class VoteContextTest(ITestOutputHelper output)
{
    [Fact]
    public void Base_Test()
    {
        var random = RandomUtility.GetRandom(output);
        var height = RandomUtility.Positive(random);
        var voteType = RandomUtility.Boolean(random) ? VoteType.PreVote : VoteType.PreCommit;
        var voteContext = new VoteContext(height, voteType, []);

        Assert.Equal(height, voteContext.Height);
        Assert.Equal(voteType, voteContext.VoteType);
        Assert.Equal(0, voteContext.Round);

        Assert.Throws<ArgumentOutOfRangeException>(() => new VoteContext(-1, voteType, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VoteContext(0, VoteType.Null, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VoteContext(0, VoteType.Unknown, []));
    }

    [Fact]
    public void Round()
    {
        var random = RandomUtility.GetRandom(output);
        var height = RandomUtility.Positive(random);
        var voteType = RandomUtility.Boolean(random) ? VoteType.PreVote : VoteType.PreCommit;
        var voteContext = new VoteContext(height, voteType, []);

        var round = RandomUtility.NonNegative(random);
        voteContext.Round = round;

        Assert.Equal(round, voteContext.Round);
        Assert.Empty(voteContext[round]);

        Assert.Throws<ArgumentOutOfRangeException>(() => voteContext.Round = RandomUtility.Negative(random));
    }

    [Fact]
    public void Indexer()
    {
        var random = RandomUtility.GetRandom(output);
        var height = RandomUtility.Positive(random);
        var voteType = RandomUtility.Boolean(random) ? VoteType.PreVote : VoteType.PreCommit;
        var voteContext = new VoteContext(height, voteType, []);
        var round = RandomUtility.NonNegative(random);
        var votes = voteContext[round];
        Assert.Empty(votes);

        Assert.Throws<ArgumentOutOfRangeException>(() => voteContext[-1]);
    }

    [Fact]
    public void Add_Throw_InvalidHeight()
    {
        var random = RandomUtility.GetRandom(output);
        var height = RandomUtility.Positive(random);
        var voteHeight = RandomUtility.Try(random, RandomUtility.Positive, item => item != height);
        var voteType = RandomUtility.Boolean(random) ? VoteType.PreVote : VoteType.PreCommit;
        var voteContext = new VoteContext(height, voteType, []);
        var vote = new VoteMetadata
        {
            Height = voteHeight,
            Round = 0,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[0]);

        var e = Assert.Throws<ArgumentException>(() => voteContext.Add(vote));
        Assert.Equal("vote", e.ParamName);
        Assert.StartsWith($"Vote height {voteHeight} does not match expected height {height}", e.Message);
    }

    [Fact]
    public void Add_Throw_InvalidRound()
    {
        var random = RandomUtility.GetRandom(output);
        var height = RandomUtility.Positive(random);
        var round = RandomUtility.Negative(random);
        var voteType = RandomUtility.Boolean(random) ? VoteType.PreVote : VoteType.PreCommit;
        var voteContext = new VoteContext(height, voteType, TestUtils.Validators);
        var vote = new VoteMetadata
        {
            Height = height,
            Round = round,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = voteType,
        }.SignWithoutValidation(TestUtils.PrivateKeys[0]);

        var e = Assert.Throws<ArgumentOutOfRangeException>(() => voteContext.Add(vote));
        Assert.Equal("round", e.ParamName);
    }

    [Fact]
    public void Add_Throw_InvalidVoteType()
    {
        var random = RandomUtility.GetRandom(output);
        var height = RandomUtility.Positive(random);
        var voteType = RandomUtility.Boolean(random) ? VoteType.PreVote : VoteType.PreCommit;
        var voteContext = new VoteContext(height, voteType, []);
        var vote = new VoteMetadata
        {
            Height = height,
            Round = 0,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = VoteType.Null,
        }.SignWithoutValidation(TestUtils.PrivateKeys[0]);

        var e = Assert.Throws<ArgumentException>(() => voteContext.Add(vote));
        Assert.Equal("vote", e.ParamName);
        Assert.StartsWith($"Vote type {vote.Type} does not match expected type {voteType}", e.Message);
    }

    [Fact]
    public void Add_Throw_UnknownValidator()
    {
        var random = RandomUtility.GetRandom(output);
        var height = RandomUtility.Positive(random);
        var voteType = RandomUtility.Boolean(random) ? VoteType.PreVote : VoteType.PreCommit;
        var voteContext = new VoteContext(height, voteType, TestUtils.Validators);
        var privateKey = RandomUtility.PrivateKey(random);
        var vote = new VoteMetadata
        {
            Height = height,
            Round = 0,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = privateKey.Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = voteType,
        }.Sign(privateKey);

        var e = Assert.Throws<ArgumentException>(() => voteContext.Add(vote));
        Assert.Equal("vote", e.ParamName);
        Assert.StartsWith($"Validator {vote.Validator} is not in the validators for height {height}", e.Message);
    }

    [Fact]
    public void Add_Throw_InvalidPower()
    {
        var random = RandomUtility.GetRandom(output);
        var height = RandomUtility.Positive(random);
        var voteType = RandomUtility.Boolean(random) ? VoteType.PreVote : VoteType.PreCommit;
        var voteContext = new VoteContext(height, voteType, TestUtils.Validators);
        var vote = new VoteMetadata
        {
            Height = height,
            Round = 0,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power + 1,
            Type = voteType,
        }.Sign(TestUtils.PrivateKeys[0]);

        var e = Assert.Throws<ArgumentException>(() => voteContext.Add(vote));
        var message = $"Validator {vote.Validator} power {vote.ValidatorPower} does not match " +
                      $"expected power {TestUtils.Validators[0].Power}";
        Assert.Equal("vote", e.ParamName);
        Assert.StartsWith(message, e.Message);
    }

    [Fact]
    public void Add()
    {
        var random = RandomUtility.GetRandom(output);
        var height = RandomUtility.Positive(random);
        var round = RandomUtility.NonNegative(random);
        var voteType = RandomUtility.Boolean(random) ? VoteType.PreVote : VoteType.PreCommit;
        var voteContext = new VoteContext(height, voteType, TestUtils.Validators);
        var vote = new VoteMetadata
        {
            Height = height,
            Round = round,
            BlockHash = RandomUtility.BlockHash(random),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = voteType,
        }.Sign(TestUtils.PrivateKeys[0]);

        voteContext.Add(vote);
        Assert.Equal(vote, voteContext[round][vote.Validator]);
    }

    // [Fact]
    // public void CannotAddMultipleVotesPerRoundPerValidator()
    // {
    //     Random random = new Random();
    //     var preVote0 = new VoteMetadata
    //     {
    //         Height = 2,
    //         Round = 0,
    //         BlockHash = default,
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = TestUtils.PrivateKeys[0].Address,
    //         ValidatorPower = TestUtils.Validators[0].Power,
    //         Type = VoteType.PreVote,
    //     }.Sign(TestUtils.PrivateKeys[0]);
    //     var preVote1 = new VoteMetadata
    //     {
    //         Height = 2,
    //         Round = 0,
    //         BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = TestUtils.PrivateKeys[0].Address,
    //         ValidatorPower = TestUtils.Validators[0].Power,
    //         Type = VoteType.PreVote,
    //     }.Sign(TestUtils.PrivateKeys[0]);
    //     var preCommit0 = new VoteMetadata
    //     {
    //         Height = 2,
    //         Round = 0,
    //         BlockHash = default,
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = TestUtils.PrivateKeys[0].Address,
    //         ValidatorPower = TestUtils.Validators[0].Power,
    //         Type = VoteType.PreCommit,
    //     }.Sign(TestUtils.PrivateKeys[0]);
    //     var preCommit1 = new VoteMetadata
    //     {
    //         Height = 2,
    //         Round = 0,
    //         BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = TestUtils.PrivateKeys[0].Address,
    //         ValidatorPower = TestUtils.Validators[0].Power,
    //         Type = VoteType.PreCommit,
    //     }.Sign(TestUtils.PrivateKeys[0]);

    //     _voteContext.Add(preVote0);
    //     Assert.Throws<DuplicateVoteException>(() => _voteContext.Add(preVote1));
    //     _voteContext.Add(preCommit0);
    //     Assert.Throws<DuplicateVoteException>(() => _voteContext.Add(preCommit1));
    // }

    // [Fact]
    // public void CannotAddVoteWithoutValidatorPower()
    // {
    //     var random = RandomUtility.GetRandom(output);
    //     var preVote = new VoteMetadata
    //     {
    //         Height = 2,
    //         Round = 0,
    //         BlockHash = default,
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Validator = TestUtils.PrivateKeys[0].Address,
    //         ValidatorPower = BigInteger.Zero,
    //         Type = VoteType.PreVote,
    //     }.Sign(TestUtils.PrivateKeys[0]);

    //     var exception = Assert.Throws<ArgumentException>(() => _voteContext.Add(preVote));
    //     Assert.Equal("ValidatorPower of the vote cannot be null", exception.Message);
    // }
}
