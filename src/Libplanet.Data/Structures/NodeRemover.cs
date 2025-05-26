using System.Diagnostics;
using Libplanet.Data.Structures.Nodes;

namespace Libplanet.Data.Structures;

internal static class NodeRemover
{
    public static INode Remove(INode node, in KeyCursor cursor) => node switch
    {
        HashNode hashNode => RemoveFromHashNode(hashNode, cursor),
        ValueNode valueNode => RemoveFromValueNode(valueNode, cursor),
        ShortNode shortNode => RemoveFromShortNode(shortNode, cursor),
        FullNode fullNode => RemoveFromFullNode(fullNode, cursor),
        _ => throw new UnreachableException($"Unsupported node value"),
    };

    private static INode RemoveFromHashNode(HashNode hashNode, KeyCursor cursor)
        => Remove(hashNode.Expand(), cursor);

    private static INode RemoveFromValueNode(ValueNode valueNode, KeyCursor cursor)
        => cursor.IsEnd ? NullNode.Value : valueNode;

    private static INode RemoveFromShortNode(ShortNode shortNode, KeyCursor cursor)
    {
        var key = shortNode.Key;
        var nextCursor = cursor.Next(cursor.Position, new(key));
        var commonLength = nextCursor.Position - cursor.Position;

        if (commonLength == key.Length)
        {
            var node = Remove(shortNode.Value, nextCursor);
            return Create(key, node);
        }

        return shortNode;

        static INode Create(string key, INode node) => node switch
        {
            ValueNode valueNode => new ShortNode { Key = key, Value = valueNode },
            FullNode fullNode => new ShortNode { Key = key, Value = fullNode },
            ShortNode shortNode => new ShortNode { Key = key + shortNode.Key, Value = shortNode.Value },
            NullNode => node,
            _ => throw new UnreachableException($"Unsupported node value"),
        };
    }

    private static INode RemoveFromFullNode(FullNode fullNode, KeyCursor cursor)
    {
        if (!cursor.IsEnd)
        {
            var index = cursor.Current;
            if (fullNode.Children[index] is { } child)
            {
                if (Remove(child, cursor.Next(1)) is { } node)
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
                return new ShortNode { Key = index + sn.Key, Value = sn.Value };
            }

            return new ShortNode { Key = index.ToString(), Value = child };
        }
        else
        {
            return fullNode;
        }
    }
}
