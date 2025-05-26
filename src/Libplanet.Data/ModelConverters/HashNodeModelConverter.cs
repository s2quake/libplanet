using System.IO;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types;

namespace Libplanet.Data.ModelConverters;

internal sealed class HashNodeModelConverter : ModelConverterBase<HashNode>
{
    protected override HashNode Deserialize(Stream stream, ModelOptions options)
    {
        var length = HashDigest<SHA256>.Size;
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        var hash = new HashDigest<SHA256>(bytes);
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

    protected override void Serialize(HashNode obj, Stream stream, ModelOptions options)
    {
        stream.Write(obj.Hash.Bytes.AsSpan());
    }
}
