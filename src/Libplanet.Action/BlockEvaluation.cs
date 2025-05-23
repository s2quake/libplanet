using Libplanet.Action;
using Libplanet.Types.Blocks;

namespace Libplanet.Action;

public sealed record class BlockEvaluation
{
    public required RawBlock Block { get; init; }

    public required World InputWorld { get; init; }

    public required World OutputWorld { get; init; }

    public ImmutableArray<ActionEvaluation> BeginEvaluations { get; init; } = [];

    public ImmutableArray<TxEvaluation> Evaluations { get; init; } = [];

    public ImmutableArray<ActionEvaluation> EndEvaluations { get; init; } = [];
}
