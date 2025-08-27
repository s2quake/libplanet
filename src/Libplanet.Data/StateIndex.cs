using System.Security.Cryptography;
using Libplanet.Data.Structures;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public partial class StateIndex(ITable table)
{
    private readonly ITable _table = table;

    public StateIndex()
        : this(new MemoryDatabase())
    {
    }

    public StateIndex(IDatabase database)
        : this(database.GetOrAdd("trie_state_store"))
    {
    }

    public bool IsEmpty => _table.Count is 0;

    public Trie GetTrie(HashDigest<SHA256> stateRootHash)
    {
        if (stateRootHash == default)
        {
            return new Trie();
        }

        if (!_table.ContainsKey(stateRootHash.ToString()))
        {
            throw new KeyNotFoundException($"State root hash {stateRootHash} not found in the state store.");
        }

        return new Trie(new HashNode { Hash = stateRootHash, Table = _table });
    }

    public Trie Commit(Trie trie)
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
        using var writeBatch = new WriteBatch(_table, 4096);
        var newNode = Commit(node, writeBatch);

        if (newNode is not HashNode)
        {
            var serialized = ModelSerializer.SerializeToBytes(newNode);
            var hashDigest = HashDigest<SHA256>.HashData(serialized);

            writeBatch.Add(hashDigest.ToString(), serialized);
            newNode = new HashNode { Hash = hashDigest, Table = _table };
        }

        return new Trie(newNode);
    }

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

        var hash = HashDigest<SHA256>.HashData(bytes);
        var key = hash.ToString();
        HashNode.AddOrUpdate(hash, node);
        writeBatch.Add(key, bytes);
        return writeBatch.Create(hash);
    }

    private sealed class WriteBatch(ITable table, int batchSize) : IDisposable
    {
        private readonly ITable _table = table;
        private readonly int _batchSize = batchSize;
        private readonly Dictionary<string, byte[]> _batch = new(batchSize);

        public void Add(string key, byte[] value)
        {
            _batch[key] = value;

            if (_batch.Count == _batchSize)
            {
                Flush();
            }
        }

        public void Flush()
        {
            foreach (var (key, value) in _batch)
            {
                _table[key] = value;
            }

            _batch.Clear();
        }

        public HashNode Create(HashDigest<SHA256> nodeHash) => new() { Hash = nodeHash, Table = _table };

        public void Dispose() => Flush();
    }
}
