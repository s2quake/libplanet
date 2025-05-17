using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Store.Trie.Nodes;
using Libplanet.Types;

namespace Libplanet.Store.Trie;

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

    public object this[in KeyBytes key]
        => NodeResolver.ResolveToValue(Node, PathCursor.Create(key))
              ?? throw new KeyNotFoundException($"Key {key} not found in the trie.");

    public static Trie Create(HashDigest<SHA256> hashDigest, ITable table)
    {
        var node = new HashNode { Hash = hashDigest, Table = table };
        return new Trie(node) { IsCommitted = true };
    }

    public static ITrie Create(params (ImmutableArray<byte> Key, object Value)[] keyValues)
        => Create(keyValues.Select(kv => (new KeyBytes(kv.Key), kv.Value)).ToArray());

    public static ITrie Create(params (KeyBytes Key, object Value)[] keyValues)
    {
        if (keyValues.Length == 0)
        {
            throw new ArgumentException("Key values cannot be empty.", nameof(keyValues));
        }

        var nibble = Nibbles.FromKeyBytes(keyValues[0].Key);
        var valueNode = new ValueNode { Value = keyValues[0].Value };
        var shortNode = new ShortNode { Key = nibble, Value = valueNode };

        ITrie trie = new Trie(shortNode);

        for (var i = 1; i < keyValues.Length; i++)
        {
            trie = trie.Set(keyValues[i].Key, keyValues[i].Value);
        }

        return trie;
    }

    public ITrie Set(in KeyBytes key, object value)
    {
        var node = Node;
        var cursor = PathCursor.Create(key);
        var valueNode = new ValueNode { Value = value };
        var newNode = NodeInserter.Insert(node, cursor, valueNode);
        return new Trie(newNode) { IsCommitted = IsCommitted };
    }

    public ITrie Remove(in KeyBytes key)
    {
        if (Node is NullNode)
        {
            throw new InvalidOperationException("Cannot remove from an empty trie.");
        }

        var cursor = PathCursor.Create(key);
        return new Trie(NodeRemover.Remove(Node, cursor));
    }

    public INode GetNode(in Nibbles key)
    {
        var node = NodeResolver.ResolveToNode(Node, new PathCursor(key));
        if (node is NullNode)
        {
            throw new KeyNotFoundException($"Key {key} not found in the trie.");
        }

        return node;
    }

    public INode GetNode(in KeyBytes key)
    {
        var nibbles = Nibbles.FromKeyBytes(key);
        return GetNode(nibbles);
    }

    public bool TryGetNode(in KeyBytes key, [MaybeNullWhen(false)] out INode node)
        => TryGetNode(Nibbles.FromKeyBytes(key), out node);

    public bool TryGetNode(in Nibbles key, [MaybeNullWhen(false)] out INode node)
    {
        node = NodeResolver.ResolveToNode(Node, new PathCursor(key));
        if (node is not NullNode)
        {
            return true;
        }

        node = null;
        return false;
    }

    public bool TryGetValue(in KeyBytes key, [MaybeNullWhen(false)] out object value)
    {
        if (NodeResolver.ResolveToValue(Node, PathCursor.Create(key)) is { } v)
        {
            value = v;
            return true;
        }

        value = null;
        return false;
    }

    public bool ContainsKey(in KeyBytes key)
    {
        try
        {
            return NodeResolver.ResolveToValue(Node, PathCursor.Create(key)) is not null;
        }
        catch
        {
            return false;
        }
    }

    public IEnumerator<KeyValuePair<KeyBytes, object>> GetEnumerator()
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
