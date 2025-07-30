using Bencodex.Types;

namespace Libplanet.Store.Trie.Nodes;

public sealed record class FullNode(ImmutableDictionary<byte, INode> Children, INode? Value)
    : INode, IEquatable<FullNode>
{
    public const byte MaximumIndex = 16;

    public ImmutableDictionary<byte, INode> Children { get; } = ValidateChildren(Children);

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

        return new(Children.SetItem(index, node), Value);
    }

    public FullNode RemoveChild(byte index)
    {
        if (index > MaximumIndex)
        {
            var message = "The index of FullNode's children should be less than 0x10.";
            throw new ArgumentOutOfRangeException(nameof(index), message);
        }

        return new(Children.Remove(index), Value);
    }

    public FullNode SetValue(INode? value) => new(Children, value);

    public bool Equals(FullNode? other)
    {
        if (other is not null)
        {
            return Children.SequenceEqual(other.Children) && Equals(Value, other.Value);
        }

        return false;
    }

    public override int GetHashCode() => Children.GetHashCode();

    public IValue ToBencodex()
    {
        var items = Enumerable.Repeat<IValue>(Null.Value, MaximumIndex + 1).ToArray();
        foreach (var (key, value) in Children)
        {
            if (value is not null)
            {
                items[key] = value.ToBencodex();
            }
        }

        if (Value is not null)
        {
            items[MaximumIndex] = Value.ToBencodex();
        }

        return new List(items);
    }

    private static ImmutableDictionary<byte, INode> ValidateChildren(
        ImmutableDictionary<byte, INode> children)
    {
        foreach (var key in children.Keys)
        {
            if (key > MaximumIndex)
            {
                var message = "The key of FullNode's children should be less than 0x10.";
                throw new ArgumentException(message, nameof(children));
            }
        }

        return children;
    }
}
