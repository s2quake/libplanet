using Libplanet.Action.State;

namespace Libplanet.Action;

public interface IAction
{
    IWorld Execute(IActionContext context);
}
