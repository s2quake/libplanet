using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Store.Trie;

public static class NodeExtensions
{
    public static IEnumerable<INode> SelfAndDescendants(this INode @this)
    {
        yield return @this;

        foreach (var child in GetChildren(@this))
        {
            yield return child;
        }
    }

    public static IEnumerable<INode> Descendants(this INode @this)
    {
        foreach (var child in @this.Children)
        {
            foreach (var descendant in child.SelfAndDescendants())
            {
                yield return descendant;
            }
        }
    }

    public static IEnumerable<KeyValuePair<KeyBytes, object>> KeyValues(this INode @this)
    {
        var nibbles = @this is ShortNode shortNode ? shortNode.Key : Nibbles.Empty;
        foreach (var item in GetKeyValues(@this, nibbles))
        {
            yield return item;
        }
    }

    private static IEnumerable<KeyValuePair<KeyBytes, object>> GetKeyValues(INode node, Nibbles nibbles)
    {
        if (node is FullNode fullNode)
        {
            if (fullNode.Value is not null)
            {
                foreach (var item in GetKeyValues(fullNode.Value, nibbles))
                {
                    yield return item;
                }
            }

            foreach (var (key, value) in fullNode.Children)
            {
                var nodeNibble = nibbles.Append(key);
                var nodeValue = value;
                foreach (var item in GetKeyValues(nodeValue, nodeNibble))
                {
                    yield return item;
                }
            }
        }
        else if (node is ShortNode shortNode)
        {
            var nodeNibbles = nibbles.Append(shortNode.Key);
            var nodeValue = shortNode.Value;
            foreach (var item in GetKeyValues(nodeValue, nodeNibbles))
            {
                yield return item;
            }
        }
        else if (node is ValueNode valueNode)
        {
            var key = nibbles.ToKeyBytes();
            var value = valueNode.Value;
            yield return new(key, value);
        }
        else if (node is HashNode hashNode)
        {
            var expandedNode = hashNode.Expand();
            foreach (var item in GetKeyValues(expandedNode, nibbles))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<INode> GetChildren(INode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var item in GetChildren(child))
            {
                yield return item;
            }
        }
    }
}
