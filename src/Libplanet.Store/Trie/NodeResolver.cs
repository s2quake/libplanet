using System.Diagnostics;
using Bencodex.Types;
using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Store.Trie;

internal static class NodeResolver
{
    public static IValue? ResolveToValue(INode? node, in PathCursor cursor) => node switch
    {
        null => null,
        ValueNode valueNode => cursor.IsEnd
            ? valueNode.Value
            : null,
        ShortNode shortNode => cursor.NextNibbles.StartsWith(shortNode.Key)
            ? ResolveToValue(shortNode.Value, cursor.Next(shortNode.Key.Length))
            : null,
        FullNode fullNode => !cursor.IsEnd
            ? ResolveToValue(fullNode.GetChild(cursor.Current), cursor.Next(1))
            : ResolveToValue(fullNode.Value, cursor),
        HashNode hashNode => ResolveToValue(hashNode.Expand(), cursor),
        NullNode _ => null,
        _ => throw new UnreachableException("An unknown type of node was encountered."),
    };

    public static INode ResolveToNode(INode node, in PathCursor cursor)
    {
        if (cursor.IsEnd)
        {
            return node;
        }

        return node switch
        {
            ValueNode _ => throw new KeyNotFoundException(
                $"Invalid node value: {node.ToBencodex().Inspect()}"),
            ShortNode shortNode
                => cursor.NextNibbles.StartsWith(shortNode.Key)
                    ? ResolveToNode(shortNode.Value, cursor.Next(shortNode.Key.Length))
                    : throw new KeyNotFoundException(
                        $"Invalid node value: {node.ToBencodex().Inspect()}"),
            FullNode fullNode
                => ResolveToNode(fullNode.Children[cursor.Current], cursor.Next(1)),
            HashNode hashNode => ResolveToNode(hashNode.Expand(), cursor),
            _ => throw new InvalidTrieNodeException(
                $"An unknown type of node was encountered " +
                $"at {cursor.SubNibbles(cursor.Position):h}: {node.GetType()}"),
        };
    }

    private static IValue ThrowKeyNotFoundException(INode node)
        => throw new KeyNotFoundException(
            $"Invalid node value: {node.ToBencodex().Inspect()}");
}
