using Libplanet.Types;

namespace Libplanet.State;

public sealed record class TransactionExecutionInfo
{
    public required Transaction Transaction { get; init; }

    public required World InputWorld { get; init; }

    public required World OutputWorld { get; init; }

    public ImmutableArray<ActionExecutionInfo> BeginExecutions { get; init; } = [];

    public ImmutableArray<ActionExecutionInfo> Executions { get; init; } = [];

    public ImmutableArray<ActionExecutionInfo> EndExecutions { get; init; } = [];
}
