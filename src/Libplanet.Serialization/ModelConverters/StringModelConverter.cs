using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class StringModelConverter : ModelConverterBase<string>
{
    protected override string Deserialize(BinaryReader reader, ModelOptions options) => reader.ReadString();

    protected override void Serialize(string obj, BinaryWriter writer, ModelOptions options) => writer.Write(obj);
}
