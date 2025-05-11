namespace Libplanet.Action;

public interface IAction
{
    void Execute(IWorldContext worldContext, IActionContext actionContext);
}
