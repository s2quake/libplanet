using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class BooleanModelConverter : ModelConverterBase<bool>
{
    protected override bool Deserialize(BinaryReader reader, ModelOptions options) => reader.ReadBoolean();

    protected override void Serialize(bool obj, BinaryWriter writer, ModelOptions options) => writer.Write(obj);
}
