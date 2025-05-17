using System.IO;
using Libplanet.Serialization;
using Libplanet.Serialization.Extensions;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.ModelConverters;

internal sealed class PublicKeyModelConverter : ModelConverterBase<PublicKey>
{
    protected override PublicKey Deserialize(Stream stream)
    {
        var length = stream.ReadInt32();
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new PublicKey(bytes);
    }

    protected override void Serialize(PublicKey obj, Stream stream) => stream.Write(obj.Bytes.AsSpan());
}
