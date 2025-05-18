using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class GuidModelConverter : ModelConverterBase<Guid>
{
    protected override Guid Deserialize(Stream stream, ModelOptions options)
    {
        var length = 16;
        var bytes = new byte[length];
        if (stream.Read(bytes, 0, length) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new Guid(bytes);
    }

    protected override void Serialize(Guid obj, Stream stream, ModelOptions options)
    {
        var bytes = obj.ToByteArray();
        stream.Write(bytes, 0, bytes.Length);
    }
}
