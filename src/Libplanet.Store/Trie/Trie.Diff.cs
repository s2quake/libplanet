using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Store.Trie;

public partial record class Trie : ITrie
{
    public IEnumerable<(KeyBytes Path, object? TargetValue, object SourceValue)> Diff(ITrie other)
    {
        if (Node is NullNode)
        {
            yield break;
        }

        var queue = new Queue<(INode, Nibbles)>();
        queue.Enqueue((Node, new Nibbles(ImmutableArray<byte>.Empty)));

        while (queue.Count > 0)
        {
            (INode node, Nibbles path) = queue.Dequeue();

            if (other.TryGetNode(path, out var targetNode))
            {
                switch (node)
                {
                    case HashNode hn:
                        // NOTE: If source node is a hashed node, check if the target node
                        // is also a hashed node and is the same.  Otherwise queue
                        // the unhashed version.
                        if (targetNode is HashNode targetHashNode && hn.Equals(targetHashNode))
                        {
                            continue;
                        }
                        else
                        {
                            queue.Enqueue((hn.Expand(), path));
                            continue;
                        }

                    default:
                        // Try comparing unhashed version of both.
                        switch (node)
                        {
                            case ValueNode valueNode:
                                var targetValue = ValueAtNodeRoot(targetNode);
                                if (targetValue is { } tv && valueNode.Value.Equals(tv))
                                {
                                    continue;
                                }
                                else
                                {
                                    yield return
                                        (path.ToKeyBytes(), targetValue, valueNode.Value);
                                    continue;
                                }

                            case ShortNode shortNode:
                                queue.Enqueue((shortNode.Value, path.Append(shortNode.Key)));
                                continue;

                            case FullNode fullNode:
                                foreach (var (index, child) in fullNode.Children)
                                {
                                    if (child is not null)
                                    {
                                        queue.Enqueue((child, path.Append(index)));
                                    }
                                }

                                if (fullNode.Value is { } value)
                                {
                                    queue.Enqueue((value, path));
                                }

                                continue;
                            default:
                                throw new InvalidTrieNodeException(
                                    $"Unknown node type encountered at {path:h}: " +
                                    $"{node.GetType()}");
                        }
                }
            }
            else
            {
                // NOTE: Target node being null at given path does not mean
                // there will not be a node at the end of an extended path.
                // Hence, we need to iterate rest of the source node.
                switch (node)
                {
                    case HashNode hashNode:
                        queue.Enqueue((hashNode.Expand(), path));
                        continue;

                    case ValueNode valueNode:
                        yield return (path.ToKeyBytes(), null, valueNode.Value);
                        continue;

                    case ShortNode shortNode:
                        queue.Enqueue((shortNode.Value, path.Append(shortNode.Key)));
                        continue;

                    case FullNode fullNode:
                        foreach (var (index, child) in fullNode.Children)
                        {
                            if (child is not null)
                            {
                                queue.Enqueue((child, path.Append(index)));
                            }
                        }

                        if (fullNode.Value is { } v)
                        {
                            queue.Enqueue((v, path));
                        }

                        continue;

                    default:
                        throw new InvalidTrieNodeException(
                            $"Unknown node type encountered at {path:h}: {node.GetType()}");
                }
            }
        }
    }

    private static object? ValueAtNodeRoot(INode node) => node switch
    {
        HashNode hashNode => ValueAtNodeRoot(hashNode.Expand()),
        ValueNode valueNode => valueNode.Value,
        FullNode fullNode => fullNode.Value is ValueNode valueNode ? valueNode.Value : null,
        NullNode => null,
        _ => null,
    };
}
