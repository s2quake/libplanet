using System.Diagnostics;
using Libplanet.Data.Structures.Nodes;

namespace Libplanet.Data.Structures;

internal static class NodeInserter
{
    public static INode Insert(INode node, in Nibbles nibbles, ValueNode value) => node switch
    {
        HashNode hashNode => InsertToHashNode(hashNode, nibbles, value),
        ValueNode valueNode => InsertToValueNode(valueNode, nibbles, value),
        ShortNode shortNode => InsertToShortNode(shortNode, nibbles, value),
        FullNode fullNode => InsertToFullNode(fullNode, nibbles, value),
        NullNode => InsertToNullNode(nibbles, value),
        _ => throw new UnreachableException($"Unsupported node value"),
    };

    private static INode InsertToNullNode(in Nibbles nibbles, ValueNode value)
        => nibbles.IsEnd ? value : new ShortNode { Key = nibbles.NextNibbles, Value = value };

    private static INode InsertToValueNode(ValueNode valueNode, in Nibbles nibbles, ValueNode value)
    {
        if (nibbles.IsEnd)
        {
            return value;
        }

        var builder = ImmutableSortedDictionary.CreateBuilder<byte, INode>();
        builder[nibbles.Current] = InsertToNullNode(nibbles.Next(1), value);
        return new FullNode { Children = builder.ToImmutable(), Value = valueNode };
    }

    private static INode InsertToShortNode(ShortNode shortNode, in Nibbles nibbles, ValueNode value)
    {
        var key = shortNode.Key;
        var nextNibbles = nibbles.Next(nibbles.Position, key);
        var commonLength = nextNibbles.Position - nibbles.Position;

        if (commonLength == key.Length)
        {
            var node = Insert(shortNode.Value, nextNibbles, value);
            return new ShortNode { Key = key, Value = node };
        }
        else
        {
            var prefix = nibbles[nibbles.Position..(nibbles.Position + commonLength)];
            var nextIndex = key[prefix.Length];
            var nextKey = key[(prefix.Length + 1)..];
            var builder = ImmutableSortedDictionary.CreateBuilder<byte, INode>();

            if (nextKey.Length > 0)
            {
                builder[nextIndex] = new ShortNode { Key = nextKey, Value = shortNode.Value };
            }
            else
            {
                builder[nextIndex] = shortNode.Value;
            }

            if (nextNibbles.Position < nextNibbles.Length)
            {
                builder[nextNibbles.Current] = InsertToNullNode(nextNibbles.Next(1), value);
            }

            var fullNode = new FullNode
            {
                Children = builder.ToImmutable(),
                Value = nextNibbles.Position >= nextNibbles.Length ? value : null,
            };

            return prefix.Length == 0 ? fullNode : new ShortNode { Key = prefix, Value = fullNode };
        }
    }

    private static FullNode InsertToFullNode(FullNode fullNode, in Nibbles nibbles, ValueNode value)
    {
        if (nibbles.IsEnd)
        {
            return new FullNode { Children = fullNode.Children, Value = value };
        }

        var index = nibbles.Current;
        if (fullNode.Children.TryGetValue(index, out var child))
        {
            var node = Insert(child, nibbles.Next(1), value);
            return fullNode.SetChild(index, node);
        }
        else
        {
            var node = InsertToNullNode(nibbles.Next(1), value);
            return fullNode.SetChild(index, node);
        }
    }

    private static INode InsertToHashNode(HashNode hashNode, in Nibbles nibbles, ValueNode value)
    {
        var unhashedNode = hashNode.Expand();
        return Insert(unhashedNode, nibbles, value);
    }
}
