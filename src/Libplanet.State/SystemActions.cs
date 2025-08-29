namespace Libplanet.State;

public sealed record class SystemActions
{
    public static SystemActions Empty { get; } = new SystemActions();

    public ImmutableArray<IAction> EnterBlockActions { get; init; } = [];

    public ImmutableArray<IAction> LeaveBlockActions { get; init; } = [];

    public ImmutableArray<IAction> EnterTxActions { get; init; } = [];

    public ImmutableArray<IAction> LeaveTxActions { get; init; } = [];
}
