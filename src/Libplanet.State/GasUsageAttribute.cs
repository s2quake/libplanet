namespace Libplanet.State;

[AttributeUsage(AttributeTargets.Class)]
public sealed class GasUsageAttribute(long amount) : Attribute
{
    public long Amount { get; } = amount;
}
