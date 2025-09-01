using System.Security.Cryptography;
using Libplanet.State.Structures;
using Libplanet.State.Structures.Nodes;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Data;

namespace Libplanet.State;

public static class StateIndexExtensions
{
    // private readonly ITable _table = table;

    // public StateIndex()
    //     : this(new MemoryDatabase())
    // {
    // }

    // public StateIndex(IDatabase database)
    //     : this(database.GetOrAdd("trie_state_store"))
    // {
    // }

    // public bool IsEmpty => _table.Count is 0;

    public static Trie GetTrie(this StateIndex stateIndex, HashDigest<SHA256> stateRootHash)
    {
        if (stateRootHash == default)
        {
            return new Trie();
        }

        if (!stateIndex.ContainsKey(stateRootHash))
        {
            throw new KeyNotFoundException($"State root hash {stateRootHash} not found in the state store.");
        }

        return new Trie(new HashNode { Hash = stateRootHash, StateIndex = stateIndex });
    }

    public static Trie Commit(this StateIndex stateIndex, Trie trie)
    {
        if (trie.Node is NullNode)
        {
            throw new ArgumentException("Empty trie cannot commit.", nameof(trie));
        }

        if (trie.Node is HashNode)
        {
            throw new ArgumentException("Committed trie cannot commit again.", nameof(trie));
        }

        var node = trie.Node;
        using var writeBatch = new WriteBatch(stateIndex, 4096);
        var newNode = Commit(node, writeBatch);

        if (newNode is not HashNode)
        {
            var value = ModelSerializer.SerializeToBytes(newNode);
            var key = HashDigest<SHA256>.HashData(value);

            writeBatch.Add(key, value);
            newNode = new HashNode { Hash = key, StateIndex = stateIndex };
        }

        return new Trie(newNode);
    }

    // public bool Contains(HashDigest<SHA256> stateRootHash)
    //     => stateRootHash != default && _table.ContainsKey(stateRootHash.ToString());

    private static INode Commit(INode node, WriteBatch writeBatch) => node switch
    {
        HashNode _ => node,
        FullNode fullNode => CommitFullNode(fullNode, writeBatch),
        ShortNode shortNode => CommitShortNode(shortNode, writeBatch),
        ValueNode valueNode => CommitValueNode(valueNode, writeBatch),
        _ => throw new NotSupportedException("Not supported node came."),
    };

    private static INode CommitFullNode(FullNode node, WriteBatch writeBatch)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<char, INode>();
        foreach (var (index, child) in node.Children)
        {
            builder.Add(index, Commit(child, writeBatch));
        }

        var virtualChildren = builder.ToImmutable();
        var newNode = new FullNode { Children = virtualChildren };
        return Write(newNode, writeBatch);
    }

    private static INode CommitShortNode(ShortNode node, WriteBatch writeBatch)
    {
        var committedValueNode = Commit(node.Value, writeBatch);
        var newNode = new ShortNode { Key = node.Key, Value = committedValueNode };
        return Write(newNode, writeBatch);
    }

    private static INode CommitValueNode(ValueNode node, WriteBatch writeBatch)
        => Write(node, writeBatch);

    private static INode Write(INode node, WriteBatch writeBatch)
    {
        var bytes = ModelSerializer.SerializeToBytes(node);
        if (bytes.Length <= HashDigest<SHA256>.Size)
        {
            return node;
        }

        var key = HashDigest<SHA256>.HashData(bytes);
        HashNode.AddOrUpdate(key, node);
        writeBatch.Add(key, bytes);
        return writeBatch.Create(key);
    }

    private sealed class WriteBatch(StateIndex stateIndex, int batchSize) : IDisposable
    {
        private readonly Dictionary<HashDigest<SHA256>, byte[]> _batch = new(batchSize);

        public void Add(HashDigest<SHA256> key, byte[] value)
        {
            _batch[key] = value;

            if (_batch.Count == batchSize)
            {
                Flush();
            }
        }

        public void Flush()
        {
            foreach (var (key, value) in _batch)
            {
                stateIndex[key] = value;
            }

            _batch.Clear();
        }

        public HashNode Create(HashDigest<SHA256> nodeHash) => new() { Hash = nodeHash, StateIndex = stateIndex };

        public void Dispose() => Flush();
    }
}
