using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class CharModelConverter : ModelConverterBase<char>
{
    protected override char Deserialize(BinaryReader reader, ModelOptions options) => reader.ReadChar();

    protected override void Serialize(char obj, BinaryWriter writer, ModelOptions options) => writer.Write(obj);
}
