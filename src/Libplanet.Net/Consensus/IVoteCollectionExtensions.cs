using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public static class IVoteCollectionExtensions
{
    public static ImmutableArray<bool> GetVoteBits(this IVoteCollection @this, BlockHash blockHash)
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
