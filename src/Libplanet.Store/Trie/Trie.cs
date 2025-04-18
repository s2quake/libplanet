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

/// <summary>
/// An <see cref="ITrie"/> implementation implemented
/// <see href="https://eth.wiki/fundamentals/patricia-tree">Merkle Patricia Trie</see>.
/// </summary>
/// <remarks>
/// An <see cref="ITrie"/> implementation.
/// </remarks>
/// <param name="keyValueStore">The <see cref="IKeyValueStore"/> storage to store
/// nodes.</param>
/// <param name="node">The root node of <see cref="Trie"/>.  If it is
/// <see langword="null"/>, it will be treated like empty trie.</param>
/// <param name="cache">The <see cref="HashNodeCache"/> to use as cache.</param>
// TODO: implement 'logs' for debugging.
public sealed partial class Trie(INode node) : ITrie
{
    private static readonly Codec _codec = new();
    private readonly NodeRemover _nodeRemover = new();
    private readonly NodeResolver _nodeResolver = new();

    /// <inheritdoc cref="ITrie.Node"/>
    public INode Node { get; } = node;

    /// <inheritdoc cref="ITrie.Hash"/>
    public HashDigest<SHA256> Hash => Node switch
    {
        null => default,
        HashNode hashNode => hashNode.Hash,
        _ => HashDigest<SHA256>.DeriveFrom(_codec.Encode(Node.ToBencodex())),
    };

    /// <inheritdoc cref="ITrie.IsCommitted"/>
    public bool IsCommitted { get; private set; } = node is not null && node is not HashNode;

    /// <inheritdoc cref="ITrie.this[KeyBytes]"/>
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

    /// <inheritdoc cref="ITrie.Set"/>
    public ITrie Set(in KeyBytes key, IValue value)
    {
        var node = Node;
        var cursor = PathCursor.Create(key);
        var valueNode = new ValueNode(value);
        var inserter = new NodeInserter();
        var newNode = inserter.Insert(node, cursor, valueNode);
        return new Trie(newNode) { IsCommitted = IsCommitted };
    }

    /// <inheritdoc cref="ITrie.Remove"/>
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

    /// <inheritdoc cref="ITrie.GetNode(Nibbles)"/>
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
