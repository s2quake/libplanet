using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class BigIntegerModelConverter : ModelConverterBase<BigInteger>
{
    protected override BigInteger Deserialize(BinaryReader reader, ModelOptions options)
    {
        var length = reader.ReadInt32();
        return new BigInteger(reader.ReadBytes(length));
    }

    protected override void Serialize(BigInteger obj, BinaryWriter writer, ModelOptions options)
    {
        var bytes = obj.ToByteArray();
        writer.Write(bytes.Length);
        writer.Write(bytes, 0, bytes.Length);
    }
}
