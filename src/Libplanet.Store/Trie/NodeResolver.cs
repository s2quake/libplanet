using System;
using System.Collections.Generic;
using Bencodex.Types;
using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Store.Trie;

internal class NodeResolver
{
    public IValue ResolveToValue(INode? node, in PathCursor cursor) => node switch
    {
        null => throw new KeyNotFoundException(
            $"Invalid node value: {node?.ToBencodex().Inspect()}"),
        ValueNode valueNode => cursor.IsEnd
            ? valueNode.Value
            : throw new KeyNotFoundException(
                $"Invalid node value: {node.ToBencodex().Inspect()}"),
        ShortNode shortNode => cursor.NextNibbles.StartsWith(shortNode.Key)
            ? ResolveToValue(shortNode.Value, cursor.Next(shortNode.Key.Length))
            : throw new KeyNotFoundException(
                $"Invalid node value: {node.ToBencodex().Inspect()}"),
        FullNode fullNode => !cursor.IsEnd
            ? ResolveToValue(fullNode.GetChild(cursor.Current), cursor.Next(1))
            : ResolveToValue(fullNode.Value, cursor),
        // HashNode hashNode => ResolveToValue(hashNode.Expand(keyValueStore), cursor),
        HashNode hashNode => throw new NotSupportedException("sdafasf "),
        _ => throw new KeyNotFoundException(
            $"Invalid node value: {node.ToBencodex().Inspect()}"),
    };

    public INode? ResolveToNode(INode? node, in PathCursor cursor)
    {
        if (cursor.IsEnd)
        {
            return node;
        }

        return node switch
        {
            null or ValueNode _ => null,
            ShortNode shortNode
                => cursor.NextNibbles.StartsWith(shortNode.Key)
                    ? ResolveToNode(shortNode.Value, cursor.Next(shortNode.Key.Length))
                    : null,
            FullNode fullNode
                => ResolveToNode(fullNode.GetChild(cursor.Current), cursor.Next(1)),
            // HashNode hashNode => ResolveToNode(hashNode.Expand(keyValueStore), cursor),
            HashNode hashNode => throw new NotSupportedException("sdafasf "),
            _ => throw new InvalidTrieNodeException(
                $"An unknown type of node was encountered " +
                $"at {cursor.SubNibbles(cursor.Position):h}: {node.GetType()}"),
        };
    }
}
