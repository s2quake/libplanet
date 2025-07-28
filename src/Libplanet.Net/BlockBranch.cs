using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;
using Microsoft.CodeAnalysis;

namespace Libplanet.Net;

public sealed record class BlockBranch : IValidatableObject
{
    public required BlockHeader BlockHeader { get; init; }

    [NotDefault]
    public ImmutableArray<Block> Blocks { get; init; } = [];

    [NotDefault]
    public ImmutableArray<BlockCommit> BlockCommits { get; init; } = [];

    public int Height => BlockHeader.Height;

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
