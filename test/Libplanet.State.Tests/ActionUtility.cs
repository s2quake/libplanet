namespace Libplanet.State.Tests;

public static class ActionUtility
{
    public static World Execute(IAction action, World world, IActionContext actionContext)
    {
        using var worldContext = new WorldContext(world);
        action.Execute(worldContext, actionContext);
        return worldContext.Flush();
    }
}
