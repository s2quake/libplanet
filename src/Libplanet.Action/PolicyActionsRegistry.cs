namespace Libplanet.Action;

public sealed record class PolicyActionsRegistry
{
    public static PolicyActionsRegistry Empty { get; } = new PolicyActionsRegistry();

    public ImmutableArray<IAction> BeginBlockActions { get; init; } = [];

    public ImmutableArray<IAction> EndBlockActions { get; init; } = [];

    public ImmutableArray<IAction> BeginTxActions { get; init; } = [];

    public ImmutableArray<IAction> EndTxActions { get; init; } = [];
}
