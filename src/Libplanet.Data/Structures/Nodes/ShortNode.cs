using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;

namespace Libplanet.Data.Structures.Nodes;

[Model(Version = 1)]
internal sealed record class ShortNode : INode, IValidatableObject
{
    [Property(0)]
    public required string Key { get; init; }

    [Property(1)]
    public required INode Value { get; init; }

    IEnumerable<INode> INode.Children => [Value];

    public override int GetHashCode()
    {
        unchecked
        {
            return (Key.GetHashCode() * 397) ^ Value.GetHashCode();
        }
    }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Key == string.Empty)
        {
            yield return new ValidationResult($"Given {nameof(Key)} cannot be empty.", [nameof(Key)]);
        }

        if (Value is ShortNode)
        {
            yield return new ValidationResult(
                $"Given {nameof(Value)} cannot be a {nameof(ShortNode)}.", [nameof(Value)]);
        }

        if (Value is HashNode hashNode && hashNode.Table is null)
        {
            yield return new ValidationResult(
                $"Given {nameof(Value)} cannot be a {nameof(HashNode)} " +
                $"without a {nameof(IDictionary<string, byte[]>)}.",
                [nameof(Value)]);
        }
    }
}
