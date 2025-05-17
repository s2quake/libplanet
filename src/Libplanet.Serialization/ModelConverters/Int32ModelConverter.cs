using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class Int32ModelConverter : ModelConverterBase<int>
{
    protected override int Deserialize(Stream stream)
    {
        var length = sizeof(int);
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return BitConverter.ToInt32(bytes);
    }

    protected override void Serialize(int obj, Stream stream)
    {
        var bytes = BitConverter.GetBytes(obj);
        stream.Write(bytes, 0, bytes.Length);
    }
}
