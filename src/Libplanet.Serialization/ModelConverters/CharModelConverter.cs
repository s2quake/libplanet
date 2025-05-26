using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class CharModelConverter : ModelConverterBase<char>
{
    protected override char Deserialize(Stream stream, ModelOptions options)
    {
        var length = sizeof(char);
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return BitConverter.ToChar(bytes);
    }

    protected override void Serialize(char obj, Stream stream, ModelOptions options)
    {
        var bytes = BitConverter.GetBytes(obj);
        stream.Write(bytes, 0, bytes.Length);
    }
}
