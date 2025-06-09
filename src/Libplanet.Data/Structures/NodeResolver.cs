using System.Diagnostics;
using Libplanet.Data.Structures.Nodes;

namespace Libplanet.Data.Structures;

internal static class NodeResolver
{
    public static object? ResolveToValue(INode? node, string key) => node switch
    {
        null => null,
        ValueNode valueNode => key.Length is 0 ? valueNode.Value : null,
        ShortNode shortNode => key.StartsWith(shortNode.Key)
            ? ResolveToValue(shortNode.Value, key[shortNode.Key.Length..])
            : null,
        FullNode fullNode => key.Length is 0
            ? ResolveToValue(fullNode.GetChildOrDefault(char.MinValue), key)
            : ResolveToValue(fullNode.GetChildOrDefault(key[0]), key[1..]),
        HashNode hashNode => ResolveToValue(hashNode.Expand(), key),
        NullNode _ => null,
        _ => throw new UnreachableException("An unknown type of node was encountered."),
    };

    public static INode ResolveToNode(INode node, string key)
        => key == string.Empty ? node : ResolveToNodeInternal(node, key);

    private static INode ResolveToNodeInternal(INode node, string key) => node switch
    {
        ValueNode => NullNode.Value,
        ShortNode shortNode => key.StartsWith(shortNode.Key)
            ? ResolveToNode(shortNode.Value, key[shortNode.Key.Length..])
            : NullNode.Value,
        FullNode fullNode=> fullNode.GetChildOrDefault(key[0]) is { } child
            ? ResolveToNode(child, key[1..])
            : NullNode.Value,
        HashNode hashNode => ResolveToNode(hashNode.Expand(), key),
        NullNode _ => NullNode.Value,
        _ => throw new UnreachableException("An unknown type of node was encountered."),
    };
}
