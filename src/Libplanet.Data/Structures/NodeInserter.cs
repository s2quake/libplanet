using System.Diagnostics;
using Libplanet.Data.Structures.Nodes;

namespace Libplanet.Data.Structures;

internal static class NodeInserter
{
    public static INode Insert(INode node, in KeyCursor cursor, ValueNode value) => node switch
    {
        HashNode hashNode => InsertToHashNode(hashNode, cursor, value),
        ValueNode valueNode => InsertToValueNode(valueNode, cursor, value),
        ShortNode shortNode => InsertToShortNode(shortNode, cursor, value),
        FullNode fullNode => InsertToFullNode(fullNode, cursor, value),
        NullNode => InsertToNullNode(cursor, value),
        _ => throw new UnreachableException($"Unsupported node value"),
    };

    private static INode InsertToNullNode(in KeyCursor cursor, ValueNode value)
        => cursor.IsEnd ? value : new ShortNode { Key = cursor.NextCursor.Key, Value = value };

    private static INode InsertToValueNode(ValueNode valueNode, in KeyCursor cursor, ValueNode value)
    {
        if (cursor.IsEnd)
        {
            return value;
        }

        var builder = ImmutableSortedDictionary.CreateBuilder<char, INode>();
        builder[cursor.Current] = InsertToNullNode(cursor.Next(1), value);
        return new FullNode { Children = builder.ToImmutable(), Value = valueNode };
    }

    private static INode InsertToShortNode(ShortNode shortNode, in KeyCursor cursor, ValueNode value)
    {
        var key = shortNode.Key;
        var nextCursor = cursor.Next(cursor.Position, new(key));
        var commonLength = nextCursor.Position - cursor.Position;

        if (commonLength == key.Length)
        {
            var node = Insert(shortNode.Value, nextCursor, value);
            return new ShortNode { Key = key, Value = node };
        }
        else
        {
            var prefix = cursor[cursor.Position..(cursor.Position + commonLength)];
            var nextIndex = key[prefix.Length];
            var nextKey = key[(prefix.Length + 1)..];
            var builder = ImmutableSortedDictionary.CreateBuilder<char, INode>();

            if (nextKey.Length > 0)
            {
                builder[nextIndex] = new ShortNode { Key = nextKey, Value = shortNode.Value };
            }
            else
            {
                builder[nextIndex] = shortNode.Value;
            }

            if (nextCursor.Position < nextCursor.Length)
            {
                builder[nextCursor.Current] = InsertToNullNode(nextCursor.Next(1), value);
            }

            var fullNode = new FullNode
            {
                Children = builder.ToImmutable(),
                Value = nextCursor.Position >= nextCursor.Length ? value : null,
            };

            return prefix.Length == 0 ? fullNode : new ShortNode { Key = prefix.Key, Value = fullNode };
        }
    }

    private static FullNode InsertToFullNode(FullNode fullNode, in KeyCursor cursor, ValueNode value)
    {
        if (cursor.IsEnd)
        {
            return new FullNode { Children = fullNode.Children, Value = value };
        }

        var index = cursor.Current;
        if (fullNode.Children.TryGetValue(index, out var child))
        {
            var node = Insert(child, cursor.Next(1), value);
            return fullNode.SetChild(index, node);
        }
        else
        {
            var node = InsertToNullNode(cursor.Next(1), value);
            return fullNode.SetChild(index, node);
        }
    }

    private static INode InsertToHashNode(HashNode hashNode, in KeyCursor cursor, ValueNode value)
    {
        var unhashedNode = hashNode.Expand();
        return Insert(unhashedNode, cursor, value);
    }
}
