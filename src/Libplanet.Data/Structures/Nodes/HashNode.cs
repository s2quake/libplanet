using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.ModelConverters;
using Libplanet.Types;

namespace Libplanet.Data.Structures.Nodes;

[ModelConverter(typeof(HashNodeModelConverter))]
internal sealed record class HashNode : INode
{
    public required HashDigest<SHA256> Hash { get; init; }

    public required ITable Table { get; init; }

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
        byte[] intermediateValue;
        if (HashNodeCache.TryGetValue(Hash, out var value))
        {
            intermediateValue = value!;
        }
        else
        {
            var key = Hash.ToString();
            if (Table.TryGetValue(key, out var valueBytes))
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
            Items = ImmutableDictionary<object, object?>.Empty.Add(typeof(ITable), Table),
        };

        var node = ModelSerializer.DeserializeFromBytes<INode>(intermediateValue, context);
        return node;
    }
}
