using System.IO;

namespace Libplanet.Serialization.StaticConverters;

internal sealed class CharModelConverter : ModelConverterBase<char>
{
    protected override char Read(BinaryReader reader, Type type, ModelOptions options) => reader.ReadChar();

    protected override void Write(BinaryWriter writer, char value, ModelOptions options) => writer.Write(value);
}
