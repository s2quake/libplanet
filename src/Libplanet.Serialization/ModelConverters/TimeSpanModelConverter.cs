using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class TimeSpanModelConverter : ModelConverterBase<TimeSpan>
{
    protected override TimeSpan Deserialize(BinaryReader reader, ModelOptions options)
        => new(reader.ReadInt64());

    protected override void Serialize(TimeSpan obj, BinaryWriter writer, ModelOptions options)
        => writer.Write(obj.Ticks);
}
