using System.IO;
using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Serialization;
using Libplanet.State.Structures.Nodes;
using Libplanet.Types;

namespace Libplanet.State.ModelConverters;

internal sealed class HashNodeModelConverter : ModelConverterBase<HashNode>
{
    protected override HashNode Deserialize(BinaryReader reader, ModelOptions options)
    {
        var length = HashDigest<SHA256>.Size;
        var hash = new HashDigest<SHA256>(reader.ReadBytes(length));
        if (!options.Items.TryGetValue(typeof(StateIndex), out var value) || value is not StateIndex table)
        {
            throw new InvalidOperationException(
                $"{nameof(StateIndex)} must be provided in {nameof(ModelOptions.Items)}.");
        }

        return new HashNode
        {
            Hash = hash,
            StateIndex = table,
        };
    }

    protected override void Serialize(HashNode obj, BinaryWriter writer, ModelOptions options)
    {
        writer.Write(obj.Hash.Bytes.AsSpan());
    }
}
