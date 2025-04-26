using Bencodex.Types;

namespace Libplanet.Action.Loader;

public interface IActionLoader
{
    IAction LoadAction(IValue value);
}
