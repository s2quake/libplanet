using Libplanet.Types;

namespace Libplanet.State;

public sealed record class TransactionExecutionInfo
{
    public required Transaction Transaction { get; init; }

    public required World EnterWorld { get; init; }

    public required World LeaveWorld { get; init; }

    public ImmutableArray<ActionExecutionInfo> EnterExecutions { get; init; } = [];

    public ImmutableArray<ActionExecutionInfo> Executions { get; init; } = [];

    public ImmutableArray<ActionExecutionInfo> LeaveExecutions { get; init; } = [];
}
