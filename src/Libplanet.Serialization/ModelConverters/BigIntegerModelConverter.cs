using System.IO;
using Libplanet.Serialization.Extensions;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class BigIntegerModelConverter : ModelConverterBase<BigInteger>
{
    protected override BigInteger Deserialize(Stream stream)
    {
        var length = stream.ReadInt32();
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new BigInteger(bytes.ToArray());
    }

    protected override void Serialize(BigInteger obj, Stream stream)
    {
        var bytes = obj.ToByteArray();
        stream.WriteInt32(bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }
}
