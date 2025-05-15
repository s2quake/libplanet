using System.Diagnostics;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Store.Trie.Nodes;

[Model(Version = 1)]
internal sealed record class HashNode : INode
{
    [Property(0)]
    public required HashDigest<SHA256> Hash { get; init; }

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

    public override int GetHashCode() => Hash.GetHashCode();

    public INode Expand()
    {
        if (Table is not { } table)
        {
            throw new InvalidOperationException(
                $"{nameof(Table)} must be set before calling {nameof(Expand)}.");
        }

        byte[] intermediateValue;
        if (HashNodeCache.TryGetValue(Hash, out var value))
        {
            intermediateValue = value!;
        }
        else
        {
            var keyBytes = new KeyBytes(Hash.Bytes);
            if (table.TryGetValue(keyBytes, out var valueBytes))
            {
                // intermediateValue = _codec.Decode(valueBytes);
                HashNodeCache.AddOrUpdate(Hash, valueBytes);
            }
            else
            {
                return NullNode.Value;
            }
        }

        throw new NotImplementedException();
        // return NodeDecoder.Decode(
        //     intermediateValue, NodeDecoder.HashEmbeddedNodeTypes, table)
        //         ?? throw new UnreachableException(
        //             $"Failed to decode the hash node with hash {Hash}.");
    }

    public byte[] Serialize()
    {
        throw new NotImplementedException();
    }
}
