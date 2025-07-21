using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "BlockResponseMessage")]
internal sealed partial record class BlockResponseMessage : MessageBase, IValidatableObject
{
    [Property(0)]
    public ImmutableArray<Block> Blocks { get; init; } = [];

    [Property(1)]
    public ImmutableArray<BlockCommit> BlockCommits { get; init; } = [];

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
            if (Blocks[i].BlockHash != BlockCommits[i].BlockHash)
            {
                yield return new ValidationResult(
                    $"Block at index {i} does not match its commit.",
                    [nameof(Blocks), nameof(BlockCommits)]);
            }

            if (Blocks[i].Height != BlockCommits[i].Height)
            {
                yield return new ValidationResult(
                    $"Block at index {i} height does not match its commit.",
                    [nameof(Blocks), nameof(BlockCommits)]);
            }
        }
    }
}
