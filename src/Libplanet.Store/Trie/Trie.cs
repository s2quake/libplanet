using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using Bencodex;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Store.Trie;

public sealed partial class Trie(INode node) : ITrie
{
    private static readonly Codec _codec = new();
    private readonly NodeRemover _nodeRemover = new();
    private readonly NodeResolver _nodeResolver = new();

    public INode Node { get; } = node;

    public HashDigest<SHA256> Hash => Node switch
    {
        HashNode hashNode => hashNode.Hash,
        _ => HashDigest<SHA256>.DeriveFrom(_codec.Encode(Node.ToBencodex())),
    };

    public bool IsCommitted { get; private set; } = node is not null && node is not HashNode;

    public IValue this[in KeyBytes key]
        => _nodeResolver.ResolveToValue(Node, PathCursor.Create(key));

    public static Trie Create(HashDigest<SHA256> hashDigest)
    {
        var node = new HashNode(hashDigest);
        return new Trie(node) { IsCommitted = true };
    }

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
        if (_nodeRemover.Remove(node, cursor) is { } newNode)
        {
            return new Trie(newNode);
        }

        return null;
    }

    public INode? GetNode(in Nibbles nibbles)
        => _nodeResolver.ResolveToNode(Node, new PathCursor(nibbles));

    public INode GetNode(in KeyBytes key)
    {
        var nibbles = Nibbles.FromKeyBytes(key);
        return GetNode(nibbles) ?? throw new KeyNotFoundException();
    }

    public bool TryGetNode(in KeyBytes key, [MaybeNullWhen(false)] out INode node)
    {
        var nibbles = Nibbles.FromKeyBytes(key);
        node = GetNode(nibbles);
        return node is not null;
    }

    public bool ContainsKey(in KeyBytes key)
        => _nodeResolver.ResolveToValue(Node, PathCursor.Create(key)) != null;

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
