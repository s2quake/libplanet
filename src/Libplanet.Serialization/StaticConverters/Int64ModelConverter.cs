using System.IO;

namespace Libplanet.Serialization.StaticConverters;

internal sealed class Int64ModelConverter : ModelConverterBase<long>
{
    protected override long Read(BinaryReader reader, Type type, ModelOptions options)
        => reader.Read7BitEncodedInt64();

    protected override void Write(BinaryWriter writer, long value, ModelOptions options)
        => writer.Write7BitEncodedInt64(value);
}
