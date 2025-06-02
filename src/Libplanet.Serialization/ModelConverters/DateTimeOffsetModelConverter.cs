using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class DateTimeOffsetModelConverter : ModelConverterBase<DateTimeOffset>
{
    protected override DateTimeOffset Deserialize(BinaryReader reader, ModelOptions options)
        => new(reader.ReadInt64(), TimeSpan.Zero);

    protected override void Serialize(DateTimeOffset obj, BinaryWriter writer, ModelOptions options)
        => writer.Write(obj.UtcTicks);
}
