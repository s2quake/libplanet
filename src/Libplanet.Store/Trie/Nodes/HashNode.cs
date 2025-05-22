using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Store.ModelConverters;
using Libplanet.Types;

namespace Libplanet.Store.Trie.Nodes;

[ModelConverter(typeof(HashNodeModelConverter))]
internal sealed record class HashNode : INode
{
    public required HashDigest<SHA256> Hash { get; init; }

    public ITable? Table { get; init; }

    IEnumerable<INode> INode.Children
    {
        get
        {
            yield return Expand();
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
            // var keyBytes = new KeyBytes(Hash.Bytes);
            var key = Hash.ToString();
            if (table.TryGetValue(key, out var valueBytes))
            {
                intermediateValue = valueBytes;
                HashNodeCache.AddOrUpdate(Hash, valueBytes);
            }
            else
            {
                return NullNode.Value;
            }
        }

        var context = new ModelOptions
        {
            Items = ImmutableDictionary<object, object?>.Empty.Add(
                typeof(ITable), table),
        };

        var node = ModelSerializer.DeserializeFromBytes<INode>(intermediateValue, context);
        return node;
    }
}
