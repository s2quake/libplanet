using System.IO;
using Libplanet.Serialization;
using Libplanet.Serialization.Extensions;

namespace Libplanet.Types.ModelConverters;

internal sealed class PublicKeyModelConverter : ModelConverterBase<PublicKey>
{
    protected override PublicKey Deserialize(Stream stream, ModelOptions options)
    {
        var length = stream.ReadInt32();
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new PublicKey(bytes);
    }

    protected override void Serialize(PublicKey obj, Stream stream, ModelOptions options)
        => stream.Write(obj.Bytes.AsSpan());
}
