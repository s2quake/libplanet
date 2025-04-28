using System.Collections;
using Bencodex.Types;

namespace Libplanet.Action.Loader;

public sealed class AggregateTypedActionLoader : IActionLoader, IEnumerable<IActionLoader>
{
    private readonly List<IActionLoader> _actionLoaderList = [];

    public AggregateTypedActionLoader()
    {
    }

    public AggregateTypedActionLoader(IActionLoader[] actionLoaders)
    {
        _actionLoaderList = [.. actionLoaders];
    }

    public IAction LoadAction(IValue value)
    {
        // if (Registry.IsSystemAction(value))
        // {
        //     return Registry.Deserialize(value);
        // }

        foreach (var item in _actionLoaderList)
        {
            try
            {
                return item.LoadAction(value);
            }
            catch
            {
                // ignored
            }
        }

        throw new ArgumentException(
            message: $"No action loader found for the given value: {value}",
            paramName: nameof(value));
    }

    public void Add(IActionLoader actionLoader) => _actionLoaderList.Add(actionLoader);

    IEnumerator<IActionLoader> IEnumerable<IActionLoader>.GetEnumerator() => _actionLoaderList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _actionLoaderList.GetEnumerator();
}
