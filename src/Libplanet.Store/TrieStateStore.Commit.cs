using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Store.Trie;
using Libplanet.Store.Trie.Nodes;
using Libplanet.Types;

namespace Libplanet.Store;

public partial class TrieStateStore
{
    private static readonly Codec _codec = new();

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
            var bencoded = newNode.ToBencodex();
            var serialized = _codec.Encode(bencoded);
            var hashDigest = HashDigest<SHA256>.Create(serialized);

            writeBatch.Add(new KeyBytes(hashDigest.Bytes), serialized);
            newNode = new HashNode { Hash = hashDigest, Table = table };
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

    private static INode CommitFullNode(FullNode fullNode, WriteBatch writeBatch)
    {
        var virtualValue = fullNode.Value is null
            ? null
            : Commit(fullNode.Value, writeBatch);
        var builder = ImmutableDictionary.CreateBuilder<byte, INode>();
        foreach (var (index, child) in fullNode.Children)
        {
            if (child is not null)
            {
                builder.Add(index, Commit(child, writeBatch));
            }
        }

        var virtualChildren = builder.ToImmutable();

        fullNode = new FullNode { Children = virtualChildren, Value = virtualValue };
        IValue encoded = fullNode.ToBencodex();

        if (encoded.EncodingLength <= HashDigest<SHA256>.Size)
        {
            return fullNode;
        }

        return Write(fullNode.ToBencodex(), writeBatch);
    }

    private static INode CommitShortNode(
        ShortNode shortNode, WriteBatch writeBatch)
    {
        // FIXME: Assumes value is not null.
        var committedValueNode = Commit(shortNode.Value, writeBatch);
        shortNode = new ShortNode { Key = shortNode.Key, Value = committedValueNode };
        IValue encoded = shortNode.ToBencodex();
        if (encoded.EncodingLength <= HashDigest<SHA256>.Size)
        {
            return shortNode;
        }

        return Write(encoded, writeBatch);
    }

    private static INode CommitValueNode(
        ValueNode valueNode, WriteBatch writeBatch)
    {
        IValue encoded = valueNode.ToBencodex();
        var nodeSize = encoded.EncodingLength;
        if (nodeSize <= HashDigest<SHA256>.Size)
        {
            return valueNode;
        }

        return Write(encoded, writeBatch);
    }

    private static HashNode Write(IValue bencodedNode, WriteBatch writeBatch)
    {
        byte[] serialized = _codec.Encode(bencodedNode);
        var nodeHash = HashDigest<SHA256>.Create(serialized);
        HashNodeCache.AddOrUpdate(nodeHash, bencodedNode);
        writeBatch.Add(new KeyBytes(nodeHash.Bytes), serialized);
        return writeBatch.Create(nodeHash);
    }

    private class WriteBatch
    {
        private readonly ITable _store;
        private readonly int _batchSize;
        private readonly Dictionary<KeyBytes, byte[]> _batch;

        public WriteBatch(ITable store, int batchSize)
        {
            _store = store;
            _batchSize = batchSize;
            _batch = new Dictionary<KeyBytes, byte[]>(_batchSize);
        }

        public bool ContainsKey(KeyBytes key) => _batch.ContainsKey(key);

        public void Add(KeyBytes key, byte[] value)
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
