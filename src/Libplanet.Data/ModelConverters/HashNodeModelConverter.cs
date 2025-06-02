using System.IO;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types;

namespace Libplanet.Data.ModelConverters;

internal sealed class HashNodeModelConverter : ModelConverterBase<HashNode>
{
    protected override HashNode Deserialize(BinaryReader reader, ModelOptions options)
    {
        var length = HashDigest<SHA256>.Size;
        var hash = new HashDigest<SHA256>(reader.ReadBytes(length));
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

    protected override void Serialize(HashNode obj, BinaryWriter writer, ModelOptions options)
    {
        writer.Write(obj.Hash.Bytes.AsSpan());
    }
}
