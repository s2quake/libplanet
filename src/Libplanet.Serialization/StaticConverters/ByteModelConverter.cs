using System.IO;

namespace Libplanet.Serialization.StaticConverters;

internal sealed class ByteModelConverter : ModelConverterBase<byte>
{
    protected override byte Read(BinaryReader reader, Type type, ModelOptions options) => reader.ReadByte();

    protected override void Write(BinaryWriter writer, byte value, ModelOptions options) => writer.Write(value);
}
