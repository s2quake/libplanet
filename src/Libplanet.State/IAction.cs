namespace Libplanet.State;

public interface IAction
{
    void Execute(IWorldContext worldContext, IActionContext actionContext);
}
