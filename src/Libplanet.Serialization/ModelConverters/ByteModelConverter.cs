using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class ByteModelConverter : ModelConverterBase<byte>
{
    protected override byte Deserialize(Stream stream)
    {
        var value = stream.ReadByte();
        return value switch
        {
            -1 => throw new EndOfStreamException("Failed to read a byte from the stream."),
            _ => checked((byte)value),
        };
    }

    protected override void Serialize(byte obj, Stream stream) => stream.WriteByte(obj);
}
