using Libplanet.Data;
using Libplanet.Data.Structures;
using Libplanet.Net.Consensus;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Libplanet.Types.Tests;

namespace Libplanet.Net.Tests.Consensus;

public class HeightVoteSetTest
{
    private readonly Blockchain _blockchain;
    private readonly VoteContext _heightVote;

    public HeightVoteSetTest()
    {
        _blockchain = TestUtils.CreateBlockChain();
        var block = _blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        _heightVote = new VoteContext(2, TestUtils.Validators);
        _blockchain.Append(block, TestUtils.CreateBlockCommit(block));
    }

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

        Assert.Throws<ArgumentException>(() => _heightVote.AddVote(preVote));
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

        Assert.Throws<ArgumentException>(() => _heightVote.AddVote(preVote));
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
        Assert.Throws<DuplicateVoteException>(
            () => _heightVote.AddVote(preCommit1));
    }

    [Fact]
    public void CannotAddVoteWithoutValidatorPower()
    {
        var preVote = new VoteMetadata
        {
            Height = 2,
            Round = 0,
            BlockHash = default,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[0].Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[0]);

        var exception = Assert.Throws<ArgumentException>(
            () => _heightVote.AddVote(preVote));
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
}
