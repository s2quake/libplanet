using System.IO;

namespace Libplanet.Serialization.StaticConverters;

internal sealed class BooleanModelConverter : ModelConverterBase<bool>
{
    protected override bool Read(BinaryReader reader, Type type, ModelOptions options) => reader.ReadBoolean();

    protected override void Write(BinaryWriter writer, bool value, ModelOptions options) => writer.Write(value);
}
