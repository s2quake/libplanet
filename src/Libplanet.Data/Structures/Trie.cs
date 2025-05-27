using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types;

namespace Libplanet.Data.Structures;

public sealed partial record class Trie(INode Node) : ITrie
{
    public Trie()
        : this(NullNode.Value)
    {
    }

    public HashDigest<SHA256> Hash => Node switch
    {
        HashNode hashNode => hashNode.Hash,
        NullNode _ => default,
        _ => HashDigest<SHA256>.Create(ModelSerializer.SerializeToBytes(Node)),
    };

    public bool IsCommitted => Node is HashNode;

    public bool IsEmpty => Node is NullNode;

    public object this[string key]
        => NodeResolver.ResolveToValue(Node, new(key))
              ?? throw new KeyNotFoundException($"Key {key} not found in the trie.");

    public static ITrie Create(params (string Key, object Value)[] keyValues)
    {
        ITrie trie = new Trie();
        for (var i = 0; i < keyValues.Length; i++)
        {
            trie = trie.Set(keyValues[i].Key, keyValues[i].Value);
        }

        return trie;
    }

    public ITrie Set(string key, object value)
    {
        var node = Node;
        var valueNode = new ValueNode { Value = value };
        var newNode = NodeInserter.Insert(node, key, valueNode);
        return new Trie(newNode);
    }

    public ITrie Remove(string key)
    {
        if (Node is NullNode)
        {
            throw new InvalidOperationException("Cannot remove from an empty trie.");
        }

        return new Trie(NodeRemover.Remove(Node, key));
    }

    public INode GetNode(string key)
    {
        var node = NodeResolver.ResolveToNode(Node, key);
        if (node is NullNode)
        {
            throw new KeyNotFoundException($"Key {key} not found in the trie.");
        }

        return node;
    }

    public bool TryGetNode(string key, [MaybeNullWhen(false)] out INode node)
    {
        node = NodeResolver.ResolveToNode(Node, key);
        if (node is not NullNode)
        {
            return true;
        }

        node = null;
        return false;
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
    {
        if (NodeResolver.ResolveToValue(Node, key) is { } v)
        {
            value = v;
            return true;
        }

        value = null;
        return false;
    }

    public bool ContainsKey(string key)
    {
        try
        {
            return NodeResolver.ResolveToValue(Node, key) is not null;
        }
        catch
        {
            return false;
        }
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        if (Node is { } node)
        {
            foreach (var item in node.KeyValues())
            {
                yield return item;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
