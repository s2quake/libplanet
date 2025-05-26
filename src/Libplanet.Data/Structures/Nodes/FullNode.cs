using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;

namespace Libplanet.Data.Structures.Nodes;

[Model(Version = 1)]
public sealed record class FullNode
    : INode, IEquatable<FullNode>, IValidatableObject
{
    [Property(0)]
    public required ImmutableSortedDictionary<char, INode> Children { get; init; }

    [Property(1)]
    public INode? Value { get; init; }

    IEnumerable<INode> INode.Children
    {
        get
        {
            if (Value is not null)
            {
                yield return Value;
            }

            foreach (var child in Children)
            {
                yield return child.Value;
            }
        }
    }

    public INode? GetChild(char index) => Children.GetValueOrDefault(index);

    public FullNode SetChild(char index, INode node)
    {
        if (node is HashNode)
        {
            var message = "FullNode cannot have a child of HashNode.";
            throw new ArgumentException(message, nameof(node));
        }

        return this with { Children = Children.SetItem(index, node) };
    }

    public FullNode RemoveChild(char index) => this with { Children = Children.Remove(index) };

    public bool Equals(FullNode? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        foreach (var (_, value) in Children)
        {
            if (value is HashNode)
            {
                yield return new ValidationResult(
                    "FullNode cannot have a child of HashNode.", [nameof(Children)]);
            }
        }
    }
}
