using System.Diagnostics;
using Libplanet.Data.Structures.Nodes;

namespace Libplanet.Data.Structures;

internal static class NodeResolver
{
    public static object? ResolveToValue(INode? node, in KeyCursor cursor) => node switch
    {
        null => null,
        ValueNode valueNode => cursor.IsEnd ? valueNode.Value : null,
        ShortNode shortNode => cursor.NextCursor.StartsWith(shortNode.Key)
            ? ResolveToValue(shortNode.Value, cursor.Next(shortNode.Key.Length))
            : null,
        FullNode fullNode => !cursor.IsEnd
            ? ResolveToValue(fullNode.GetChild(cursor.Current), cursor.Next(1))
            : ResolveToValue(fullNode.Value, cursor),
        HashNode hashNode => ResolveToValue(hashNode.Expand(), cursor),
        NullNode _ => null,
        _ => throw new UnreachableException("An unknown type of node was encountered."),
    };

    public static INode ResolveToNode(INode node, in KeyCursor cursor)
    {
        if (cursor.IsEnd)
        {
            return node;
        }

        return node switch
        {
            ValueNode => NullNode.Value,
            ShortNode shortNode
                => cursor.NextCursor.StartsWith(shortNode.Key)
                    ? ResolveToNode(shortNode.Value, cursor.Next(shortNode.Key.Length))
                    : NullNode.Value,
            FullNode fullNode
                => fullNode.GetChild(cursor.Current) is { } child
                    ? ResolveToNode(child, cursor.Next(1))
                    : NullNode.Value,
            HashNode hashNode => ResolveToNode(hashNode.Expand(), cursor),
            NullNode _ => NullNode.Value,
            _ => throw new UnreachableException("An unknown type of node was encountered."),
        };
    }
}
