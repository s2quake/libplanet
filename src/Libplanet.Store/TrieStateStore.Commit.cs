using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Store.Trie.Nodes;
using Libplanet.Types;

namespace Libplanet.Store;

public partial class TrieStateStore
{
    public ITrie Commit(ITrie trie)
    {
        var root = trie.Node;
        if (root is NullNode)
        {
            return trie;
        }

        var writeBatch = new WriteBatch(_table, 4096);
        var newNode = Commit(root, writeBatch);

        if (newNode is not HashNode)
        {
            var serialized = ModelSerializer.SerializeToBytes(newNode);
            var hashDigest = HashDigest<SHA256>.Create(serialized);

            writeBatch.Add(hashDigest.ToString(), serialized);
            newNode = new HashNode { Hash = hashDigest, Table = _table };
        }

        writeBatch.Flush();

        return new Trie.Trie(newNode);
    }

    private static INode Commit(INode node, WriteBatch writeBatch) => node switch
    {
        // NOTE: If it is a hashed node, it has been recorded already.
        HashNode _ => node,
        FullNode fullNode => CommitFullNode(fullNode, writeBatch),
        ShortNode shortNode => CommitShortNode(shortNode, writeBatch),
        ValueNode valueNode => CommitValueNode(valueNode, writeBatch),
        _ => throw new NotSupportedException("Not supported node came."),
    };

    private static INode CommitFullNode(FullNode node, WriteBatch writeBatch)
    {
        var virtualValue = node.Value is null ? null : Commit(node.Value, writeBatch);
        var builder = ImmutableSortedDictionary.CreateBuilder<byte, INode>();
        foreach (var (index, child) in node.Children)
        {
            if (child is not null)
            {
                builder.Add(index, Commit(child, writeBatch));
            }
        }

        var virtualChildren = builder.ToImmutable();
        var newNode = new FullNode { Children = virtualChildren, Value = virtualValue };
        var bytes = ModelSerializer.SerializeToBytes(newNode);
        if (bytes.Length <= HashDigest<SHA256>.Size)
        {
            return newNode;
        }

        return Write(bytes, writeBatch);
    }

    private static INode CommitShortNode(ShortNode node, WriteBatch writeBatch)
    {
        var committedValueNode = Commit(node.Value, writeBatch);
        var newNode = new ShortNode { Key = node.Key, Value = committedValueNode };
        var bytes = ModelSerializer.SerializeToBytes(newNode);
        if (bytes.Length <= HashDigest<SHA256>.Size)
        {
            return newNode;
        }

        return Write(bytes, writeBatch);
    }

    private static INode CommitValueNode(ValueNode node, WriteBatch writeBatch)
    {
        var bytes = ModelSerializer.SerializeToBytes(node);
        if (bytes.Length <= HashDigest<SHA256>.Size)
        {
            return node;
        }

        return Write(bytes, writeBatch);
    }

    private static HashNode Write(byte[] bytes, WriteBatch writeBatch)
    {
        var hash = HashDigest<SHA256>.Create(bytes);
        var key = hash.ToString();
        HashNodeCache.AddOrUpdate(hash, bytes);
        writeBatch.Add(key, bytes);
        return writeBatch.Create(hash);
    }

    private sealed class WriteBatch
    {
        private readonly ITable _store;
        private readonly int _batchSize;
        private readonly Dictionary<string, byte[]> _batch;

        public WriteBatch(ITable store, int batchSize)
        {
            _store = store;
            _batchSize = batchSize;
            _batch = new Dictionary<string, byte[]>(_batchSize);
        }

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
            _store.SetMany(_batch);
            _batch.Clear();
        }

        public HashNode Create(HashDigest<SHA256> nodeHash)
        {
            return new HashNode { Hash = nodeHash, Table = _store };
        }
    }
}
