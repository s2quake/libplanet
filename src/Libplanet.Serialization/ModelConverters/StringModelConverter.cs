using System.IO;
using System.Text;
using Libplanet.Serialization.Extensions;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class StringModelConverter : ModelConverterBase<string>
{
    protected override string Deserialize(Stream stream, ModelOptions options)
    {
        var length = stream.ReadInt32();
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    protected override void Serialize(string obj, Stream stream, ModelOptions options)
    {
        var bytes = Encoding.UTF8.GetBytes(obj);
        stream.WriteInt32(bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }
}
