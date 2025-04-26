using Libplanet.Action.State;

namespace Libplanet.Action;

public sealed record class ActionEvaluation
{
    public required IAction Action { get; init; }

    public required IActionContext InputContext { get; init; }

    public required IWorld OutputState { get; init; }

    public Exception? Exception { get; init; }
}
