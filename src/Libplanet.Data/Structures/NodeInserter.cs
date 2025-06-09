using System.Diagnostics;
using Libplanet.Data.Structures.Nodes;

namespace Libplanet.Data.Structures;

internal static class NodeInserter
{
    public static INode Insert(INode node, string key, ValueNode value) => node switch
    {
        HashNode hashNode => InsertToHashNode(hashNode, key, value),
        ValueNode valueNode => InsertToValueNode(valueNode, key, value),
        ShortNode shortNode => InsertToShortNode(shortNode, key, value),
        FullNode fullNode => InsertToFullNode(fullNode, key, value),
        NullNode => InsertToNullNode(key, value),
        _ => throw new UnreachableException($"Unsupported node value"),
    };

    private static INode InsertToNullNode(string key, ValueNode value)
        => key == string.Empty ? value : new ShortNode { Key = key, Value = value };

    private static INode InsertToValueNode(ValueNode valueNode, string key, ValueNode value)
    {
        if (key == string.Empty)
        {
            return value;
        }

        var builder = ImmutableSortedDictionary.CreateBuilder<char, INode>();
        var nextKey = key[1..];
        builder[char.MinValue] = valueNode;
        builder[key[0]] = InsertToNullNode(nextKey, value);

        return new FullNode { Children = builder.ToImmutable() };
    }

    private static INode InsertToShortNode(ShortNode shortNode, string key, ValueNode value)
    {
        var oldKey = shortNode.Key;
        var prefix = oldKey.GetCommonPrefix(key);
        var nextKey = key[prefix.Length..];

        if (prefix.Length == oldKey.Length)
        {
            var node = Insert(shortNode.Value, nextKey, value);
            return new ShortNode { Key = oldKey, Value = node };
        }
        else
        {
            var builder = ImmutableSortedDictionary.CreateBuilder<char, INode>();

            var oldKey2 = oldKey[prefix.Length..];
            var oldNextKey2 = oldKey2[1..];
            if (oldNextKey2 != string.Empty)
            {
                builder[oldKey2[0]] = new ShortNode { Key = oldNextKey2, Value = shortNode.Value };
            }
            else
            {
                builder[oldKey2[0]] = shortNode.Value;
            }

            if (nextKey != string.Empty)
            {
                builder[nextKey[0]] = InsertToNullNode(nextKey[1..], value);
            }

            if (key.Length == prefix.Length)
            {
                builder[char.MinValue] = value;
            }

            var fullNode = new FullNode
            {
                Children = builder.ToImmutable(),
            };

            return prefix.Length == 0 ? fullNode : new ShortNode { Key = prefix, Value = fullNode };
        }
    }

    private static FullNode InsertToFullNode(FullNode fullNode, string key, ValueNode value)
    {
        if (key == string.Empty)
        {
            return new FullNode { Children = fullNode.Children.Add(char.MinValue, value) };
        }

        var index = key[0];
        if (fullNode.Children.TryGetValue(index, out var child))
        {
            var node = Insert(child, key[1..], value);
            return fullNode.SetChild(index, node);
        }
        else
        {
            var node = InsertToNullNode(key[1..], value);
            return fullNode.SetChild(index, node);
        }
    }

    private static INode InsertToHashNode(HashNode hashNode, string key, ValueNode value)
    {
        var unhashedNode = hashNode.Expand();
        return Insert(unhashedNode, key, value);
    }
}
