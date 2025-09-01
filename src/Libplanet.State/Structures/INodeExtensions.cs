using Libplanet.State.Structures.Nodes;

namespace Libplanet.State.Structures;

public static class INodeExtensions
{
    public static IEnumerable<INode> Traverse(this INode @this)
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
            foreach (var descendant in child.Traverse())
            {
                yield return descendant;
            }
        }
    }

    public static IEnumerable<KeyValuePair<string, object>> KeyValues(this INode @this)
    {
        foreach (var item in GetKeyValues(@this, string.Empty))
        {
            yield return item;
        }
    }

    private static IEnumerable<KeyValuePair<string, object>> GetKeyValues(INode node, string key)
    {
        if (node is FullNode fullNode)
        {
            foreach (var (k, v) in fullNode.Children)
            {
                var nodeKey = key + k;
                var nodeValue = v;
                foreach (var item in GetKeyValues(nodeValue, nodeKey))
                {
                    yield return item;
                }
            }
        }
        else if (node is ShortNode shortNode)
        {
            var nodeKey = key + shortNode.Key;
            var nodeValue = shortNode.Value;
            foreach (var item in GetKeyValues(nodeValue, nodeKey))
            {
                yield return item;
            }
        }
        else if (node is ValueNode valueNode)
        {
            var k = key;
            var v = valueNode.Value;
            yield return new(k, v);
        }
        else if (node is HashNode hashNode)
        {
            var expandedNode = hashNode.Expand();
            foreach (var item in GetKeyValues(expandedNode, key))
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
