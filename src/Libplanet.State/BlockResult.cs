using Libplanet.Types.Blocks;

namespace Libplanet.State;

public sealed record class BlockResult
{
    public required RawBlock Block { get; init; }

    public required World InputWorld { get; init; }

    public required World OutputWorld { get; init; }

    public ImmutableArray<ActionResult> BeginEvaluations { get; init; } = [];

    public ImmutableArray<TransactionResult> Evaluations { get; init; } = [];

    public ImmutableArray<ActionResult> EndEvaluations { get; init; } = [];
}
