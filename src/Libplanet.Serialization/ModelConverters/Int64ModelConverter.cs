using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class Int64ModelConverter : ModelConverterBase<long>
{
    protected override long Deserialize(BinaryReader reader, ModelOptions options) => reader.ReadInt64();

    protected override void Serialize(long obj, BinaryWriter writer, ModelOptions options) => writer.Write(obj);
}
