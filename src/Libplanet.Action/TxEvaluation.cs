using Libplanet.Action;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

public sealed record class TxEvaluation
{
    public required Transaction Transaction { get; init; }

    public required World InputWorld { get; init; }

    public required World OutputWorld { get; init; }

    public ImmutableArray<ActionEvaluation> BeginEvaluations { get; init; } = [];

    public ImmutableArray<ActionEvaluation> Evaluations { get; init; } = [];

    public ImmutableArray<ActionEvaluation> EndEvaluations { get; init; } = [];
}
