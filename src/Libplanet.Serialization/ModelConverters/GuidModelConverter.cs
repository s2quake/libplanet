using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class GuidModelConverter : ModelConverterBase<Guid>
{
    protected override Guid Deserialize(BinaryReader reader, ModelOptions options)
        => new(reader.ReadBytes(16));

    protected override void Serialize(Guid obj, BinaryWriter writer, ModelOptions options)
        => writer.Write(obj.ToByteArray());
}
