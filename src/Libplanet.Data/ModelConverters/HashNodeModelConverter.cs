using System.IO;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types;

namespace Libplanet.Data.ModelConverters;

internal sealed class HashNodeModelConverter : ModelConverterBase<HashNode>
{
    protected override HashNode Deserialize(ref ModelReader reader, ModelOptions options)
    {
        var hash = new HashDigest<SHA256>(reader.ReadBytes());
        if (!options.Items.TryGetValue(typeof(ITable), out var value) || value is not ITable table)
        {
            throw new InvalidOperationException(
                $"{nameof(ITable)} must be provided in {nameof(ModelOptions.Items)}.");
        }

        return new HashNode
        {
            Hash = hash,
            Table = table,
        };
    }

    protected override void Serialize(HashNode obj, ref ModelWriter writer, ModelOptions options)
    {
        writer.Write(obj.Hash.Bytes.AsSpan());
    }
}
