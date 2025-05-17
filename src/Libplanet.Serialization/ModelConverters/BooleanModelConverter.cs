using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class BooleanModelConverter : ModelConverterBase<bool>
{
    protected override bool Deserialize(Stream stream, ModelContext context)
    {
        var value = stream.ReadByte();
        return value switch
        {
            -1 => throw new EndOfStreamException("Failed to read a byte from the stream."),
            0 => false,
            1 => true,
            _ => throw new InvalidDataException($"Invalid boolean value: {value}. Expected 0 or 1."),
        };
    }

    protected override void Serialize(bool obj, Stream stream, ModelContext context)
        => stream.WriteByte(obj ? (byte)1 : (byte)0);
}
