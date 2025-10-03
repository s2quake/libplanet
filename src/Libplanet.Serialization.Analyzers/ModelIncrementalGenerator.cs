using Libplanet.Serialization.Analyzers.Items;
using Microsoft.CodeAnalysis;

namespace Libplanet.Serialization.Analyzers;

[Generator]
public sealed class ModelIncrementalGenerator : IIncrementalGenerator
{
    private readonly ItemBase[] _items =
    {
        new LIBP1001(),
        new LIBP1002(),
        new LIBP1003(),
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        foreach (var item in _items)
        {
            item.Initialize(context);
        }
    }
}
