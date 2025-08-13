using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public static class IVoteCollectionExtensions
{
    public static ImmutableArray<bool> GetBits(this IVoteCollection @this, BlockHash blockHash)
    {
        var bitList = new List<bool>(@this.Validators.Count);
        foreach (var validator in @this.Validators)
        {
            if (@this.TryGetValue(validator.Address, out var vote) && vote.BlockHash == blockHash)
            {
                bitList.Add(true);
            }
            else
            {
                bitList.Add(false);
            }
        }

        return [.. bitList];
    }

    public static ImmutableArray<Vote> GetVotes(this IVoteCollection @this, ImmutableArray<bool> bits)
    {
        // if (voteBits.Height != Height)
        // {
        //     throw new ArgumentException(
        //         $"VoteSetBits height {voteBits.Height} does not match expected height {Height}.",
        //         nameof(voteBits));
        // }

        // if (voteBits.VoteType is not VoteType.PreVote and not VoteType.PreCommit)
        // {
        //     throw new ArgumentException("VoteType should be either PreVote or PreCommit.", nameof(voteBits));
        // }
        var validators = @this.Validators;

        if (bits.Length != validators.Count)
        {
            throw new ArgumentException(
                $"Bits length {bits.Length} does not match validators count {validators.Count}.",
                nameof(bits));
        }

        // var round = _rounds[voteBits.Round];
        // var bits = voteBits.Bits;
        // var votes = voteBits.VoteType is VoteType.PreVote ? round.PreVotes : round.PreCommits;

        var voteList = new List<Vote>(validators.Count);
        for (var i = 0; i < bits.Length; i++)
        {
            if (!bits[i] && @this.TryGetValue(validators[i].Address, out var vote))
            {
                voteList.Add(vote);
            }
        }

        return [.. voteList];
    }

    public static BlockCommit GetBlockCommit(this IVoteCollection @this)
    {
        var decidedBlockHash = @this.GetMajority23();
        var query = from validator in @this.Validators
                    let key = validator.Address
                    let vote = @this.TryGetValue(key, out var vote) ? vote : new VoteMetadata
                    {
                        Height = @this.Height,
                        Round = @this.Round,
                        BlockHash = decidedBlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = key,
                        ValidatorPower = validator.Power,
                        Type = VoteType.Null,
                    }.WithoutSignature()
                    where vote.BlockHash == decidedBlockHash
                    select vote;

        return new BlockCommit
        {
            Height = @this.Height,
            Round = @this.Round,
            BlockHash = decidedBlockHash,
            Votes = [.. query],
        };
    }
}
