using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class BlockBranch : IValidatableObject
{
    public static BlockBranch Empty { get; } = new()
    {
        Blocks = [],
        BlockCommits = [],
    };

    [NotDefault]
    public ImmutableArray<Block> Blocks { get; init; } = [];

    [NotDefault]
    public ImmutableArray<BlockCommit> BlockCommits { get; init; } = [];

    public static BlockBranch Create(params (Block, BlockCommit)[] blockPairs) => new()
    {
        Blocks = [.. blockPairs.Select(item => item.Item1)],
        BlockCommits = [.. blockPairs.Select(item => item.Item2)],
    };

    public BlockBranch TakeAfter(Block branchPoint)
    {
        var index = Blocks.IndexOf(branchPoint);
        var i = index >= 0 ? index + 1 : int.MaxValue;
        return new BlockBranch
        {
            Blocks = Blocks[i..],
            BlockCommits = BlockCommits[i..],
        };
    }

    public bool Equals(BlockBranch? other) => other switch
    {
        null => false,
        _ => Blocks.SequenceEqual(other.Blocks) && BlockCommits.SequenceEqual(other.BlockCommits),
    };

    public override int GetHashCode() => HashCode.Combine(Blocks, BlockCommits);

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Blocks.Length != BlockCommits.Length)
        {
            yield return new ValidationResult(
                "The number of blocks must match the number of block commits.",
                [nameof(Blocks), nameof(BlockCommits)]);
        }

        for (var i = 0; i < Blocks.Length; i++)
        {
            if (BlockCommits[i] != BlockCommit.Empty && Blocks[i].BlockHash != BlockCommits[i].BlockHash)
            {
                yield return new ValidationResult(
                    $"Block at index {i} does not match its commit.",
                    [nameof(Blocks), nameof(BlockCommits)]);
            }
        }
    }
}
