using Libplanet.Types;

namespace Libplanet.State;

public sealed record class TransactionExecution
{
    public required Transaction Transaction { get; init; }

    public required World EnterWorld { get; init; }

    public required World LeaveWorld { get; init; }

    public ImmutableArray<ActionExecution> EnterExecutions { get; init; } = [];

    public ImmutableArray<ActionExecution> Executions { get; init; } = [];

    public ImmutableArray<ActionExecution> LeaveExecutions { get; init; } = [];
}
