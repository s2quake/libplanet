using System.IO;

namespace Libplanet.Serialization.StaticConverters;

internal sealed class StringModelConverter : ModelConverterBase<string>
{
    protected override string? Read(BinaryReader reader, Type type, ModelOptions options) => reader.ReadString();

    protected override void Write(BinaryWriter writer, string value, ModelOptions options)
        => writer.Write(value);
}
