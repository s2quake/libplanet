using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class Int32ModelConverter : ModelConverterBase<int>
{
    protected override int Deserialize(BinaryReader reader, ModelOptions options) => reader.ReadInt32();

    protected override void Serialize(int obj, BinaryWriter writer, ModelOptions options) => writer.Write(obj);
}
