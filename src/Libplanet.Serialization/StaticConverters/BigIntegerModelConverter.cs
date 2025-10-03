using System.IO;

namespace Libplanet.Serialization.StaticConverters;

internal sealed class BigIntegerModelConverter : ModelConverterBase<BigInteger>
{
    protected override BigInteger Read(BinaryReader reader, Type type, ModelOptions options)
    {
        var length = reader.ReadInt32();
        return new BigInteger(reader.ReadBytes(length));
    }

    protected override void Write(BinaryWriter writer, BigInteger value, ModelOptions options)
    {
        var bytes = value.ToByteArray();
        writer.Write(bytes.Length);
        writer.Write(bytes, 0, bytes.Length);
    }
}
