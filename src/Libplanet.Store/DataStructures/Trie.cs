using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Store.DataStructures.Nodes;
using Libplanet.Types;

namespace Libplanet.Store.DataStructures;

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

    public bool IsCommitted { get; private set; } = Node is HashNode or NullNode;

    public object this[string key]
        => NodeResolver.ResolveToValue(Node, Nibbles.Create(key))
              ?? throw new KeyNotFoundException($"Key {key} not found in the trie.");

    public static ITrie Create(params (string Key, object Value)[] keyValues)
    {
        if (keyValues.Length == 0)
        {
            throw new ArgumentException("Key values cannot be empty.", nameof(keyValues));
        }

        var nibbles = Nibbles.Create(keyValues[0].Key);
        var valueNode = new ValueNode { Value = keyValues[0].Value };
        var shortNode = new ShortNode { Key = nibbles, Value = valueNode };

        ITrie trie = new Trie(shortNode);

        for (var i = 1; i < keyValues.Length; i++)
        {
            trie = trie.Set(keyValues[i].Key, keyValues[i].Value);
        }

        return trie;
    }

    public ITrie Set(string key, object value)
    {
        var node = Node;
        var nibbles = Nibbles.Create(key);
        var valueNode = new ValueNode { Value = value };
        var newNode = NodeInserter.Insert(node, nibbles, valueNode);
        return new Trie(newNode) { IsCommitted = IsCommitted };
    }

    public ITrie Remove(string key)
    {
        if (Node is NullNode)
        {
            throw new InvalidOperationException("Cannot remove from an empty trie.");
        }

        var nibbles = Nibbles.Create(key);
        return new Trie(NodeRemover.Remove(Node, nibbles));
    }

    public INode GetNode(string key)
    {
        var nibbles = Nibbles.Create(key);
        var node = NodeResolver.ResolveToNode(Node, nibbles);
        if (node is NullNode)
        {
            throw new KeyNotFoundException($"Key {key} not found in the trie.");
        }

        return node;
    }

    public bool TryGetNode(string key, [MaybeNullWhen(false)] out INode node)
    {
        var nibbles = Nibbles.Create(key);
        node = NodeResolver.ResolveToNode(Node, nibbles);
        if (node is not NullNode)
        {
            return true;
        }

        node = null;
        return false;
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
    {
        if (NodeResolver.ResolveToValue(Node, Nibbles.Create(key)) is { } v)
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
            return NodeResolver.ResolveToValue(Node, Nibbles.Create(key)) is not null;
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

    internal static Trie Create(HashDigest<SHA256> hashDigest, ITable table)
    {
        var node = new HashNode { Hash = hashDigest, Table = table };
        return new Trie(node) { IsCommitted = true };
    }
}
