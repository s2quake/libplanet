using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class ByteModelConverter : ModelConverterBase<byte>
{
    protected override byte Deserialize(BinaryReader reader, ModelOptions options) => reader.ReadByte();

    protected override void Serialize(byte obj, BinaryWriter writer, ModelOptions options) => writer.Write(obj);
}
