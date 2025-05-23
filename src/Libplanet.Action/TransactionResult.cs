using Libplanet.Types.Transactions;

namespace Libplanet.Action;

public sealed record class TransactionResult
{
    public required Transaction Transaction { get; init; }

    public required World InputWorld { get; init; }

    public required World OutputWorld { get; init; }

    public ImmutableArray<ActionResult> BeginEvaluations { get; init; } = [];

    public ImmutableArray<ActionResult> Evaluations { get; init; } = [];

    public ImmutableArray<ActionResult> EndEvaluations { get; init; } = [];
}
