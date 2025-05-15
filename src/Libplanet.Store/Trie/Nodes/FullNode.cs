using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;

namespace Libplanet.Store.Trie.Nodes;

[Model(Version = 1)]
public sealed record class FullNode
    : INode, IEquatable<FullNode>, IValidatableObject
{
    public const byte MaximumIndex = 16;

    [Property(0)]
    public required ImmutableSortedDictionary<byte, INode> Children { get; init; }

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

    public INode? GetChild(byte index)
    {
        if (index > MaximumIndex)
        {
            var message = "The index of FullNode's children should be less than 0x10.";
            throw new ArgumentOutOfRangeException(nameof(index), message);
        }

        return Children.GetValueOrDefault(index);
    }

    public FullNode SetChild(byte index, INode node)
    {
        if (index > MaximumIndex)
        {
            var message = "The index of FullNode's children should be less than 0x10.";
            throw new ArgumentOutOfRangeException(nameof(index), message);
        }

        if (node is HashNode)
        {
            var message = "FullNode cannot have a child of HashNode.";
            throw new ArgumentException(message, nameof(node));
        }

        return this with { Children = Children.SetItem(index, node) };
    }

    public FullNode RemoveChild(byte index)
    {
        if (index > MaximumIndex)
        {
            var message = "The index of FullNode's children should be less than 0x10.";
            throw new ArgumentOutOfRangeException(nameof(index), message);
        }

        return this with { Children = Children.Remove(index) };
    }

    public FullNode SetValue(INode? value) => this with { Value = value };

    public bool Equals(FullNode? other)
    {
        if (other is not null)
        {
            return Children.SequenceEqual(other.Children) && Equals(Value, other.Value);
        }

        return false;
    }

    public override int GetHashCode() => Children.GetHashCode();

    public byte[] Serialize()
    {
        throw new NotImplementedException();
    }

    // public IValue ToBencodex()
    // {
    //     var items = Enumerable.Repeat<IValue>(null, MaximumIndex + 1).ToArray();
    //     foreach (var (key, value) in Children)
    //     {
    //         if (value is not null)
    //         {
    //             items[key] = value.ToBencodex();
    //         }
    //     }

    //     if (Value is not null)
    //     {
    //         items[MaximumIndex] = Value.ToBencodex();
    //     }

    //     return new List(items);
    // }

    // private static ImmutableDictionary<byte, INode> ValidateChildren(
    //     ImmutableDictionary<byte, INode> children)
    // {
    //     foreach (var key in children.Keys)
    //     {
    //         if (key > MaximumIndex)
    //         {
    //             var message = "The key of FullNode's children should be less than 0x10.";
    //             throw new ArgumentException(message, nameof(children));
    //         }
    //     }

    //     return children;
    // }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        foreach (var (key, value) in Children)
        {
            if (key > MaximumIndex)
            {
                yield return new ValidationResult(
                    "The key of FullNode's children should be less than 0x10.", [nameof(Children)]);
            }

            if (value is HashNode)
            {
                yield return new ValidationResult(
                    "FullNode cannot have a child of HashNode.", [nameof(Children)]);
            }
        }
    }
}
