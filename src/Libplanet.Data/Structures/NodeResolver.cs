using System.Diagnostics;
using Libplanet.Data.Structures.Nodes;

namespace Libplanet.Data.Structures;

internal static class NodeResolver
{
    public static object? ResolveToValue(INode? node, in Nibbles nibbles) => node switch
    {
        null => null,
        ValueNode valueNode => nibbles.IsEnd ? valueNode.Value : null,
        ShortNode shortNode => nibbles.NextNibbles.StartsWith(shortNode.Key)
            ? ResolveToValue(shortNode.Value, nibbles.Next(shortNode.Key.Length))
            : null,
        FullNode fullNode => !nibbles.IsEnd
            ? ResolveToValue(fullNode.GetChild(nibbles.Current), nibbles.Next(1))
            : ResolveToValue(fullNode.Value, nibbles),
        HashNode hashNode => ResolveToValue(hashNode.Expand(), nibbles),
        NullNode _ => null,
        _ => throw new UnreachableException("An unknown type of node was encountered."),
    };

    public static INode ResolveToNode(INode node, in Nibbles nibbles)
    {
        if (nibbles.IsEnd)
        {
            return node;
        }

        return node switch
        {
            ValueNode => NullNode.Value,
            ShortNode shortNode
                => nibbles.NextNibbles.StartsWith(shortNode.Key)
                    ? ResolveToNode(shortNode.Value, nibbles.Next(shortNode.Key.Length))
                    : NullNode.Value,
            FullNode fullNode
                => fullNode.GetChild(nibbles.Current) is { } child
                    ? ResolveToNode(child, nibbles.Next(1))
                    : NullNode.Value,
            HashNode hashNode => ResolveToNode(hashNode.Expand(), nibbles),
            NullNode _ => NullNode.Value,
            _ => throw new UnreachableException("An unknown type of node was encountered."),
        };
    }
}
