using System.IO;

namespace Libplanet.Serialization.StaticConverters;

internal sealed class GuidModelConverter : ModelConverterBase<Guid>
{
    protected override Guid Read(BinaryReader reader, Type type, ModelOptions options)
        => new(reader.ReadBytes(16));

    protected override void Write(BinaryWriter writer, Guid value, ModelOptions options)
        => writer.Write(value.ToByteArray());
}
