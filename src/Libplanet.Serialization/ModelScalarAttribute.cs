using Libplanet.Serialization.DynamicConverters;

namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ModelScalarAttribute(string typeName)
    : ModelConverterAttribute(typeof(ModelScalarConverter), typeName)
{
    public ModelScalarKind Kind { get; init; }
}
