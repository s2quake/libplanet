namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Property)]
public sealed class PropertyAttribute(int index) : Attribute
{
    public int Index { get; } = index;
}
