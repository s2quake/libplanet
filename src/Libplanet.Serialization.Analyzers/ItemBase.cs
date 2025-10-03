using Microsoft.CodeAnalysis;

namespace Libplanet.Serialization.Analyzers;

internal abstract class ItemBase
{
    public abstract void Initialize(IncrementalGeneratorInitializationContext context);
}
