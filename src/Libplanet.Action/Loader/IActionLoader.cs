namespace Libplanet.Action.Loader;

public interface IActionLoader
{
    IAction LoadAction(byte[] value);
}
