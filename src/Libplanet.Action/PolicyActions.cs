namespace Libplanet.Action;

public sealed record class PolicyActions
{
    public static PolicyActions Empty { get; } = new PolicyActions();

    public ImmutableArray<IAction> BeginBlockActions { get; init; } = [];

    public ImmutableArray<IAction> EndBlockActions { get; init; } = [];

    public ImmutableArray<IAction> BeginTxActions { get; init; } = [];

    public ImmutableArray<IAction> EndTxActions { get; init; } = [];
}
