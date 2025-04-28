using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Store.Trie;

public sealed partial record class Trie(INode Node) : ITrie
{
    private static readonly Codec _codec = new();

    public HashDigest<SHA256> Hash => Node switch
    {
        HashNode hashNode => hashNode.Hash,
        _ => HashDigest<SHA256>.DeriveFrom(_codec.Encode(Node.ToBencodex())),
    };

    public bool IsCommitted { get; private set; } = Node is HashNode;

    public IValue this[in KeyBytes key]
        => NodeResolver.ResolveToValue(Node, PathCursor.Create(key))
              ?? throw new KeyNotFoundException($"Key {key} not found in the trie.");

    public static Trie Create(HashDigest<SHA256> hashDigest, IKeyValueStore keyValueStore)
    {
        var node = new HashNode(hashDigest) { KeyValueStore = keyValueStore };
        return new Trie(node) { IsCommitted = true };
    }

    public static ITrie Create(params (ImmutableArray<byte> Key, IValue Value)[] keyValues)
        => Create(keyValues.Select(kv => (new KeyBytes(kv.Key), kv.Value)).ToArray());

    public static ITrie Create(params (KeyBytes Key, IValue Value)[] keyValues)
    {
        if (keyValues.Length == 0)
        {
            throw new ArgumentException("Key values cannot be empty.", nameof(keyValues));
        }

        var nibble = Nibbles.FromKeyBytes(keyValues[0].Key);
        var valueNode = new ValueNode(keyValues[0].Value);
        var shortNode = new ShortNode(nibble, valueNode);

        ITrie trie = new Trie(shortNode);

        for (var i = 1; i < keyValues.Length; i++)
        {
            trie = trie.Set(keyValues[i].Key, keyValues[i].Value);
        }

        return trie;
    }

    public ITrie Set(in KeyBytes key, IValue value)
    {
        var node = Node;
        var cursor = PathCursor.Create(key);
        var valueNode = new ValueNode(value);
        var newNode = NodeInserter.Insert(node, cursor, valueNode);
        return new Trie(newNode) { IsCommitted = IsCommitted };
    }

    public ITrie? Remove(in KeyBytes key)
    {
        if (Node is not { } node)
        {
            throw new InvalidOperationException("Cannot remove from an empty trie.");
        }

        var cursor = PathCursor.Create(key);
        if (NodeRemover.Remove(node, cursor) is { } newNode)
        {
            return new Trie(newNode);
        }

        return null;
    }

    public INode GetNode(in Nibbles key)
        => NodeResolver.ResolveToNode(Node, new PathCursor(key));

    public INode GetNode(in KeyBytes key)
    {
        var nibbles = Nibbles.FromKeyBytes(key);
        return GetNode(nibbles) ?? throw new KeyNotFoundException();
    }

    public bool TryGetNode(in KeyBytes key, [MaybeNullWhen(false)] out INode node)
    {
        var nibbles = Nibbles.FromKeyBytes(key);
        try
        {
            node = GetNode(nibbles);
            return true;
        }
        catch (KeyNotFoundException)
        {
            node = null;
            return false;
        }
    }

    public bool TryGetValue(in KeyBytes key, [MaybeNullWhen(false)] out IValue value)
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

    public IEnumerator<KeyValuePair<KeyBytes, IValue>> GetEnumerator()
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
