using Bencodex.Types;
using Libplanet.Serialization;

namespace Libplanet.Action.Loader;

public sealed class SingleActionLoader<T> : IActionLoader
    where T : IAction
{
    public Type Type => typeof(T);

    public IAction LoadAction(IValue value) => ModelSerializer.Deserialize<T>(value);
}
