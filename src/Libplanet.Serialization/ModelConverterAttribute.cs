namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ModelConverterAttribute(Type converterType, string typeName) : Attribute
{
    public Type ConverterType { get; } = converterType;

    public string TypeName { get; } = typeName;
}
