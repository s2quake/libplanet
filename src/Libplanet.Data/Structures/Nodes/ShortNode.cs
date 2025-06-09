using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Data.Structures.Nodes;

[Model(Version = 1, TypeName = "snode")]
internal sealed record class ShortNode : INode, IValidatableObject
{
    [Property(0)]
    [NotEmpty]
    public required string Key { get; init; }

    [Property(1)]
    public required INode Value { get; init; }

    IEnumerable<INode> INode.Children => [Value];

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Value is not FullNode and not ValueNode and not HashNode)
        {
            yield return new ValidationResult(
                $"Given {nameof(Value)} is unexpected type: {Value.GetType().Name}.", [nameof(Value)]);
        }
    }
}
