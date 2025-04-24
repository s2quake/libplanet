using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;

namespace Libplanet.Store.Trie.Nodes;

internal sealed record class HashNode(in HashDigest<SHA256> HashDigest) : INode
{
    private static readonly Codec _codec = new();

    public HashDigest<SHA256> Hash { get; } = HashDigest;

    public IKeyValueStore? KeyValueStore { get; init; }

    IEnumerable<INode> INode.Children
    {
        get
        {
            foreach (var item in Expand().Children)
            {
                yield return item;
            }
        }
    }

    public IValue ToBencodex() => Hash.Bencoded;

    public override int GetHashCode() => Hash.GetHashCode();

    public INode Expand()
    {
        if (KeyValueStore is not { } keyValueStore)
        {
            throw new InvalidOperationException(
                $"{nameof(KeyValueStore)} must be set before calling {nameof(Expand)}.");
        }

        IValue intermediateValue;
        if (HashNodeCache.TryGetValue(Hash, out var value))
        {
            intermediateValue = value!;
        }
        else
        {
            var keyBytes = new KeyBytes(Hash.ByteArray);
            intermediateValue = _codec.Decode(keyValueStore[keyBytes]);
            HashNodeCache.AddOrUpdate(Hash, intermediateValue);
        }

        return NodeDecoder.Decode(
            intermediateValue, NodeDecoder.HashEmbeddedNodeTypes, keyValueStore);
    }
}
