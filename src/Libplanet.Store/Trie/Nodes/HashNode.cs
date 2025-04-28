using System.Diagnostics;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Serialization;

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

    public IValue ToBencodex() => ModelSerializer.Serialize(Hash);

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
            var keyBytes = new KeyBytes(Hash.Bytes);
            if (keyValueStore.TryGetValue(keyBytes, out var valueBytes))
            {
                intermediateValue = _codec.Decode(keyValueStore[keyBytes]);
                HashNodeCache.AddOrUpdate(Hash, intermediateValue);
            }
            else
            {
                return NullNode.Value;
            }
        }

        return NodeDecoder.Decode(
            intermediateValue, NodeDecoder.HashEmbeddedNodeTypes, keyValueStore)
                ?? throw new UnreachableException(
                    $"Failed to decode the hash node with hash {Hash}.");
    }
}
