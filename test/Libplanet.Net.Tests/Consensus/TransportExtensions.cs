using Libplanet.Net.Messages;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public static class TransportExtensions
{
    public static Vote PostPreVote(
        this ITransport @this,
        Peer peer,
        int validator,
        Block block,
        int round = 0)
    {
        var preVote = new VoteBuilder
        {
            Validator = Validators[validator],
            Block = block,
            Round = round,
            Type = VoteType.PreVote,
        }.Create(Signers[validator]);
        @this.Post(peer, new ConsensusPreVoteMessage { PreVote = preVote });
        return preVote;
    }

    public static Vote PostPreVote(
        this ITransport @this,
        Peer peer,
        int validator,
        BlockHash blockHash,
        int height,
        int round = 0)
    {
        var preVote = new VoteMetadata
        {
            Validator = Validators[validator].Address,
            ValidatorPower = Validators[validator].Power,
            Height = height,
            BlockHash = blockHash,
            Round = round,
            Type = VoteType.PreVote,
        }.Sign(Signers[validator]);
        @this.Post(peer, new ConsensusPreVoteMessage { PreVote = preVote });
        return preVote;
    }

    public static Vote PostNilPreVote(
        this ITransport @this,
        Peer peer,
        int validator,
        int height,
        int round = 0)
    {
        var preVote = new VoteMetadata
        {
            Validator = Validators[validator].Address,
            ValidatorPower = Validators[validator].Power,
            Height = height,
            BlockHash = default,
            Round = round,
            Type = VoteType.PreVote,
        }.Sign(Signers[validator]);
        @this.Post(peer, new ConsensusPreVoteMessage { PreVote = preVote });
        return preVote;
    }

    public static Vote PostPreCommit(
        this ITransport @this,
        Peer peer,
        int validator,
        Block block,
        int round = 0)
    {
        var preVote = new VoteBuilder
        {
            Validator = Validators[validator],
            Block = block,
            Round = round,
            Type = VoteType.PreCommit,
        }.Create(Signers[validator]);
        @this.Post(peer, new ConsensusPreCommitMessage { PreCommit = preVote });
        return preVote;
    }

    public static Vote PostPreCommit(
        this ITransport @this,
        Peer peer,
        int validator,
        BlockHash blockHash,
        int height,
        int round = 0)
    {
        var preVote = new VoteMetadata
        {
            Validator = Validators[validator].Address,
            ValidatorPower = Validators[validator].Power,
            Height = height,
            BlockHash = blockHash,
            Round = round,
            Type = VoteType.PreCommit,
        }.Sign(Signers[validator]);
        @this.Post(peer, new ConsensusPreCommitMessage { PreCommit = preVote });
        return preVote;
    }
    
    public static Vote PostNilPreCommit(
        this ITransport @this,
        Peer peer,
        int validator,
        int height,
        int round = 0)
    {
        var preVote = new VoteMetadata
        {
            Validator = Validators[validator].Address,
            ValidatorPower = Validators[validator].Power,
            Height = height,
            BlockHash = default,
            Round = round,
            Type = VoteType.PreCommit,
        }.Sign(Signers[validator]);
        @this.Post(peer, new ConsensusPreCommitMessage { PreCommit = preVote });
        return preVote;
    }
}