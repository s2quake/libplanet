using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public sealed class Branch
{
    public Branch(IEnumerable<(Block, BlockCommit)> blocks)
    {
        ImmutableArray<(Block, BlockCommit)> sorted =
            blocks.OrderBy(block => block.Item1.Height).ToImmutableArray();
        if (!sorted.Any())
        {
            throw new ArgumentException(
                $"Given {nameof(blocks)} must not be empty.", nameof(blocks));
        }
        else if (!sorted
                     .Zip(
                         sorted.Skip(1),
                         (prev, next) =>
                             prev.Item1.Height + 1 == next.Item1.Height &&
                             prev.Item1.Hash.Equals(next.Item1.PreviousHash))
                     .All(pred => pred))
        {
            throw new ArgumentException(
                $"Given {nameof(blocks)} must be consecutive.",
                nameof(blocks));
        }

        Blocks = sorted;
    }

    public ImmutableArray<(Block, BlockCommit)> Blocks { get; }
}
