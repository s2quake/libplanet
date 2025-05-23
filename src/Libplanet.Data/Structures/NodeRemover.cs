using System.Diagnostics;
using Libplanet.Data.Structures.Nodes;

namespace Libplanet.Data.Structures;

internal static class NodeRemover
{
    public static INode Remove(INode node, in Nibbles nibbles) => node switch
    {
        HashNode hashNode => RemoveFromHashNode(hashNode, nibbles),
        ValueNode valueNode => RemoveFromValueNode(valueNode, nibbles),
        ShortNode shortNode => RemoveFromShortNode(shortNode, nibbles),
        FullNode fullNode => RemoveFromFullNode(fullNode, nibbles),
        _ => throw new UnreachableException($"Unsupported node value"),
    };

    private static INode RemoveFromHashNode(HashNode hashNode, Nibbles nibbles)
        => Remove(hashNode.Expand(), nibbles);

    private static INode RemoveFromValueNode(ValueNode valueNode, Nibbles nibbles)
        => nibbles.IsEnd ? NullNode.Value : valueNode;

    private static INode RemoveFromShortNode(ShortNode shortNode, Nibbles nibbles)
    {
        var key = shortNode.Key;
        var nextNibbles = nibbles.Next(nibbles.Position, key);
        var commonLength = nextNibbles.Position - nibbles.Position;

        if (commonLength == key.Length)
        {
            var node = Remove(shortNode.Value, nextNibbles);
            return Create(key, node);
        }

        return shortNode;

        static INode Create(Nibbles key, INode node) => node switch
        {
            ValueNode valueNode => new ShortNode { Key = key, Value = valueNode },
            FullNode fullNode => new ShortNode { Key = key, Value = fullNode },
            ShortNode shortNode => new ShortNode { Key = key.Append(shortNode.Key), Value = shortNode.Value },
            NullNode => node,
            _ => throw new UnreachableException($"Unsupported node value"),
        };
    }

    private static INode RemoveFromFullNode(FullNode fullNode, Nibbles nibbles)
    {
        if (!nibbles.IsEnd)
        {
            var index = nibbles.Current;
            if (fullNode.Children[index] is { } child)
            {
                if (Remove(child, nibbles.Next(1)) is { } node)
                {
                    if (node is NullNode)
                    {
                        return fullNode.RemoveChild(index);
                    }

                    return fullNode.SetChild(index, node);
                }

                return ReduceFullNode(fullNode.RemoveChild(index));
            }

            return fullNode;
        }

        return ReduceFullNode(new FullNode { Children = fullNode.Children });
    }

    private static INode ReduceFullNode(FullNode fullNode)
    {
        var children = fullNode.Children;
        if (children.Count == 0)
        {
            if (fullNode.Value is not null)
            {
                return fullNode.Value;
            }

            throw new ArgumentException(
                $"Given {nameof(fullNode)} must have at least 1 child: {children.Count}", nameof(fullNode));
        }
        else if (children.Count == 1)
        {
            var (index, child) = children.Single();
            child = child is HashNode hn ? hn.Expand() : child;
            if (child is ShortNode sn)
            {
                return new ShortNode { Key = new Nibbles([index]).Append(sn.Key), Value = sn.Value };
            }

            return new ShortNode { Key = new Nibbles([index]), Value = child };
        }
        else
        {
            return fullNode;
        }
    }
}
