using System;
using System.Diagnostics;
using System.Linq;
using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Store.Trie;

internal static class NodeRemover
{
    public static INode? Remove(INode node, in PathCursor cursor) => node switch
    {
        HashNode hashNode => RemoveFromHashNode(hashNode, cursor),
        ValueNode valueNode => RemoveFromValueNode(valueNode, cursor),
        ShortNode shortNode => RemoveFromShortNode(shortNode, cursor),
        FullNode fullNode => RemoveFromFullNode(fullNode, cursor),
        _ => throw new UnreachableException(
            $"Unsupported node value: {node.ToBencodex().Inspect()}"),
    };

    private static INode? RemoveFromHashNode(HashNode hashNode, PathCursor cursor)
        => Remove(hashNode.Expand(), cursor);

    private static ValueNode? RemoveFromValueNode(ValueNode valueNode, PathCursor cursor)
        => cursor.IsEnd ? null : valueNode;

    private static ShortNode? RemoveFromShortNode(ShortNode shortNode, PathCursor cursor)
    {
        var key = shortNode.Key;
        var nextCursor = cursor.SkipCommonPrefix(cursor.Position, key);
        var commonLength = nextCursor.Position - cursor.Position;

        if (commonLength == key.Length)
        {
            if (Remove(shortNode.Value, nextCursor) is { } node)
            {
                return Create(key, node);
            }

            return null;
        }

        return shortNode;

        static ShortNode Create(Nibbles key, INode node) => node switch
        {
            ValueNode valueNode => new ShortNode(key, valueNode),
            FullNode fullNode => new ShortNode(key, fullNode),
            ShortNode shortNode => new ShortNode(key.Append(shortNode.Key), shortNode.Value),
            _ => throw new UnreachableException(
                    $"Unsupported node value: {node.ToBencodex().Inspect()}"),
        };
    }

    private static INode RemoveFromFullNode(FullNode fullNode, PathCursor cursor)
    {
        if (!cursor.IsEnd)
        {
            var nextNibble = cursor.Current;
            if (fullNode.Children[nextNibble] is { } child)
            {
                if (Remove(child, cursor.Next(1)) is { } node)
                {
                    return fullNode.SetChild(nextNibble, node);
                }

                return ReduceFullNode(fullNode.RemoveChild(nextNibble));
            }

            return fullNode;
        }

        return ReduceFullNode(new FullNode(fullNode.Children, null));
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
            else
            {
                throw new ArgumentException(
                $"Given {nameof(fullNode)} must have at least 1 child: {children.Count}");
            }
        }
        else if (children.Count == 1)
        {
            var (index, child) = children.Single();
            child = child is HashNode hn ? hn.Expand() : child;
            return child is ShortNode sn
                    ? new ShortNode(new Nibbles([index]).Append(sn.Key), sn.Value)
                    : new ShortNode(new Nibbles([index]), child);
        }
        else
        {
            return fullNode;
        }
    }
}
