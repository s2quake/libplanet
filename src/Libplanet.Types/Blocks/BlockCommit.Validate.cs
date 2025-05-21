using System.ComponentModel.DataAnnotations;
using Libplanet.Types.Consensus;

namespace Libplanet.Types.Blocks;

public sealed partial record class BlockCommit : IValidatableObject
{
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        var height = Height;
        var round = Round;
        var blockHash = BlockHash;

        if (Votes.Any(vote =>
            vote.Height != height ||
            vote.Round != round ||
            !blockHash.Equals(vote.BlockHash) ||
            (vote.Flag != VoteFlag.Null && vote.Flag != VoteFlag.PreCommit) ||
            (vote.Flag == VoteFlag.PreCommit && !ValidationUtility.TryValidate(vote))))
        {
            yield return new ValidationResult(
                $"Every vote must have the same height as {Height}, the same round " +
                $"as {Round}, the same hash as {BlockHash}, and must have flag value of " +
                $"either {VoteFlag.Null} or {VoteFlag.PreCommit}, " +
                $"and must be signed if the vote's flag is {VoteFlag.PreCommit}.",
                [nameof(Votes)]);
        }
    }

    // internal void ValidateBlockCommit(Block block, BlockCommit blockCommit)
    // {
    //     if (block.Height == 0)
    //     {
    //         if (blockCommit is { })
    //         {
    //             throw new InvalidOperationException(
    //                 "Genesis block does not have blockCommit.");
    //         }
    //         else
    //         {
    //             return;
    //         }
    //     }

    //     if (block.Height != 0 && blockCommit == default)
    //     {
    //         throw new InvalidOperationException(
    //             $"Block #{block.BlockHash} BlockCommit is required except for the genesis block.");
    //     }

    //     if (block.Height != blockCommit.Height)
    //     {
    //         throw new InvalidOperationException(
    //             "BlockCommit has height value that is not same with block height. " +
    //             $"Block height is {block.Height}, however, BlockCommit height is " +
    //             $"{blockCommit.Height}.");
    //     }

    //     if (!block.BlockHash.Equals(blockCommit.BlockHash))
    //     {
    //         throw new InvalidOperationException(
    //             $"BlockCommit has different block. Block hash is {block.BlockHash}, " +
    //             $"however, BlockCommit block hash is {blockCommit.BlockHash}.");
    //     }

    //     var validators = GetWorld(block.StateRootHash).GetValidatorSet();
    //     validators.ValidateBlockCommitValidators(blockCommit);
    //     BigInteger commitPower = blockCommit.Votes.Aggregate(
    //         BigInteger.Zero,
    //         (power, vote) => power + (vote.Flag == VoteFlag.PreCommit
    //             ? validators.GetValidator(vote.Validator).Power
    //             : BigInteger.Zero));
    //     if (validators.GetTwoThirdsPower() >= commitPower)
    //     {
    //         throw new InvalidOperationException(
    //             $"BlockCommit of BlockHash {blockCommit.BlockHash} " +
    //             $"has insufficient vote power {commitPower} compared to 2/3 of " +
    //             $"the total power {validators.GetTotalPower()}");
    //     }
    // }
}
