using Bencodex.Types;

namespace Libplanet.Action;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ActionTypeAttribute(string typeIdentifier) : Attribute
{
    public IValue TypeIdentifier { get; } = new Text(typeIdentifier);
}
