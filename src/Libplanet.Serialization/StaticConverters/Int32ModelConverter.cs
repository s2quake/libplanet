using System.IO;

namespace Libplanet.Serialization.StaticConverters;

internal sealed class Int32ModelConverter : ModelConverterBase<int>
{
    protected override int Read(BinaryReader reader, Type type, ModelOptions options)
        => reader.Read7BitEncodedInt();

    protected override void Write(BinaryWriter writer, int value, ModelOptions options)
        => writer.Write7BitEncodedInt(value);
}
