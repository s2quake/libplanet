namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ModelConverterAttribute(Type converterType) : Attribute
{
    public Type ConverterType { get; } = converterType;
}
