using System.Diagnostics;
using Libplanet.State.Structures.Nodes;

namespace Libplanet.State.Structures;

internal static class NodeRemover
{
    public static INode Remove(INode node, string key) => node switch
    {
        HashNode hashNode => RemoveFromHashNode(hashNode, key),
        ValueNode valueNode => RemoveFromValueNode(valueNode, key),
        ShortNode shortNode => RemoveFromShortNode(shortNode, key),
        FullNode fullNode => RemoveFromFullNode(fullNode, key),
        _ => throw new UnreachableException($"Unsupported node value"),
    };

    private static INode RemoveFromHashNode(HashNode hashNode, string key) => Remove(hashNode.Expand(), key);

    private static INode RemoveFromValueNode(ValueNode valueNode, string key)
        => key.Length is 0 ? NullNode.Value : valueNode;

    private static INode RemoveFromShortNode(ShortNode shortNode, string key)
    {
        var oldKey = shortNode.Key;
        var prefix = oldKey.GetCommonPrefix(key);
        var nextCursor = key[prefix.Length..];

        if (!key.StartsWith(shortNode.Key))
        {
            throw new KeyNotFoundException(
                $"Key '{key}' does not start with the short node key '{shortNode.Key}'.");
        }

        if (prefix.Length == oldKey.Length)
        {
            var node = Remove(shortNode.Value, nextCursor);
            return Create(oldKey, node);
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

    private static INode RemoveFromFullNode(FullNode fullNode, string key)
    {
        if (key != string.Empty)
        {
            var index = key[0];
            var child = fullNode.Children[index];
            var node = Remove(child, key[1..]);
            if (node is NullNode)
            {
                return ReduceFullNode(fullNode.RemoveChild(index));
            }

            return fullNode.SetChild(index, node);
        }

        return ReduceFullNode(fullNode.RemoveChild(char.MinValue));
    }

    private static INode ReduceFullNode(FullNode fullNode)
    {
        var children = fullNode.Children;
        if (children.Count is 0)
        {
            throw new UnreachableException("FullNode should not have zero children.");
        }
        else if (children.Count is 1)
        {
            var (index, child) = children.Single();
            if (index == char.MinValue)
            {
                if (child is HashNode hashNode)
                {
                    return hashNode.Expand();
                }

                return child;
            }

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
