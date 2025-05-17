using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class ByteModelConverter : ModelConverterBase<byte>
{
    protected override byte Deserialize(Stream stream, ModelContext context)
    {
        var value = stream.ReadByte();
        return value switch
        {
            -1 => throw new EndOfStreamException("Failed to read a byte from the stream."),
            _ => checked((byte)value),
        };
    }

    protected override void Serialize(byte obj, Stream stream, ModelContext context)
        => stream.WriteByte(obj);
}
