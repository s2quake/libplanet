using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class Int64ModelConverter : ModelConverterBase<long>
{
    protected override long Deserialize(Stream stream)
    {
        var length = sizeof(long);
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return BitConverter.ToInt64(bytes);
    }

    protected override void Serialize(long obj, Stream stream)
    {
        var bytes = BitConverter.GetBytes(obj);
        stream.Write(bytes, 0, bytes.Length);
    }
}
