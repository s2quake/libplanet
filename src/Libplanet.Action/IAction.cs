using Libplanet.Action.State;

namespace Libplanet.Action;

public interface IAction
{
    World Execute(IActionContext context);
}
