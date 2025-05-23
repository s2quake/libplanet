namespace Libplanet.Action;

public sealed record class SystemActions
{
    public static SystemActions Empty { get; } = new SystemActions();

    public ImmutableArray<IAction> BeginBlockActions { get; init; } = [];

    public ImmutableArray<IAction> EndBlockActions { get; init; } = [];

    public ImmutableArray<IAction> BeginTxActions { get; init; } = [];

    public ImmutableArray<IAction> EndTxActions { get; init; } = [];
}
