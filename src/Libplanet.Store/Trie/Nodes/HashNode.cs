using System.Diagnostics;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Types;

namespace Libplanet.Store.Trie.Nodes;

internal sealed record class HashNode(in HashDigest<SHA256> HashDigest) : INode
{
    private static readonly Codec _codec = new();

    public HashDigest<SHA256> Hash { get; } = HashDigest;

    public ITable? Table { get; init; }

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

    public IValue ToBencodex() => new Binary(Hash.Bytes);

    public override int GetHashCode() => Hash.GetHashCode();

    public INode Expand()
    {
        if (Table is not { } table)
        {
            throw new InvalidOperationException(
                $"{nameof(Table)} must be set before calling {nameof(Expand)}.");
        }

        IValue intermediateValue;
        if (HashNodeCache.TryGetValue(Hash, out var value))
        {
            intermediateValue = value!;
        }
        else
        {
            var keyBytes = new KeyBytes(Hash.Bytes);
            if (table.TryGetValue(keyBytes, out var valueBytes))
            {
                intermediateValue = _codec.Decode(table[keyBytes]);
                HashNodeCache.AddOrUpdate(Hash, intermediateValue);
            }
            else
            {
                return NullNode.Value;
            }
        }

        return NodeDecoder.Decode(
            intermediateValue, NodeDecoder.HashEmbeddedNodeTypes, table)
                ?? throw new UnreachableException(
                    $"Failed to decode the hash node with hash {Hash}.");
    }
}
