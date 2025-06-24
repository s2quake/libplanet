using Libplanet.Net.Consensus;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus;

public sealed class HeightVoteTest(ITestOutputHelper output)
{
    private readonly HeightContext _heightVote = new(2, TestUtils.Validators);

    [Fact]
    public void CannotAddDifferentHeight()
    {
        var preVote = new VoteMetadata
        {
            Height = 3,
            Round = 0,
            BlockHash = default,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[0]);

        var e = Assert.Throws<ArgumentException>(() => _heightVote.AddVote(preVote));
        Assert.Equal("vote", e.ParamName);
    }

    [Fact]
    public void CannotAddUnknownValidator()
    {
        var key = new PrivateKey();
        var preVote = new VoteMetadata
        {
            Height = 2,
            Round = 0,
            BlockHash = default,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = key.Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreVote,
        }.Sign(key);

        var e = Assert.Throws<ArgumentException>(() => _heightVote.AddVote(preVote));
        Assert.Equal("vote", e.ParamName);
    }

    [Fact]
    public void CannotAddValidatorWithInvalidPower()
    {
        var preVote = new VoteMetadata
        {
            Height = 2,
            Round = 0,
            BlockHash = default,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.Validators[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power + 1,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[0]);

        Assert.Throws<ArgumentException>(() => _heightVote.AddVote(preVote));
    }

    [Fact]
    public void CannotAddMultipleVotesPerRoundPerValidator()
    {
        Random random = new Random();
        var preVote0 = new VoteMetadata
        {
            Height = 2,
            Round = 0,
            BlockHash = default,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[0]);
        var preVote1 = new VoteMetadata
        {
            Height = 2,
            Round = 0,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[0]);
        var preCommit0 = new VoteMetadata
        {
            Height = 2,
            Round = 0,
            BlockHash = default,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.PrivateKeys[0]);
        var preCommit1 = new VoteMetadata
        {
            Height = 2,
            Round = 0,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.PrivateKeys[0]);

        _heightVote.AddVote(preVote0);
        Assert.Throws<DuplicateVoteException>(() => _heightVote.AddVote(preVote1));
        _heightVote.AddVote(preCommit0);
        Assert.Throws<DuplicateVoteException>(() => _heightVote.AddVote(preCommit1));
    }

    [Fact]
    public void CannotAddVoteWithoutValidatorPower()
    {
        var random = RandomUtility.GetRandom(output);
        var preVote = new VoteMetadata
        {
            Height = 2,
            Round = 0,
            BlockHash = default,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = BigInteger.Zero,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[0]);

        var exception = Assert.Throws<InvalidVoteException>(() => _heightVote.AddVote(preVote));
        Assert.Equal("ValidatorPower of the vote cannot be null", exception.Message);
    }

    [Fact]
    public void GetCount()
    {
        var preVotes = Enumerable.Range(0, TestUtils.PrivateKeys.Count)
            .Select(
                index => new VoteMetadata
                {
                    Height = 2,
                    Round = 0,
                    BlockHash = default,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = TestUtils.PrivateKeys[index].Address,
                    ValidatorPower = TestUtils.Validators[index].Power,
                    Type = VoteType.PreVote,
                }.Sign(TestUtils.PrivateKeys[index]))
            .ToList();
        var preCommit = new VoteMetadata
        {
            Height = 2,
            Round = 0,
            BlockHash = default,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.PrivateKeys[0]);

        foreach (var preVote in preVotes)
        {
            _heightVote.AddVote(preVote);
        }

        _heightVote.AddVote(preCommit);

        Assert.Equal(5, _heightVote.GetVotes(0, VoteType.PreVote).Count +
            _heightVote.GetVotes(0, VoteType.PreCommit).Count);
    }

    private static Blockchain CreateDummyBlockChain()
    {
        var blockchain = TestUtils.CreateDummyBlockChain();
        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));
        return blockchain;
    }
}
