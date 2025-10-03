using System.IO;

namespace Libplanet.Serialization.StaticConverters;

internal sealed class TimeSpanModelConverter : ModelConverterBase<TimeSpan>
{
    protected override TimeSpan Read(BinaryReader reader, Type type, ModelOptions options)
        => new(reader.Read7BitEncodedInt64());

    protected override void Write(BinaryWriter writer, TimeSpan value, ModelOptions options)
        => writer.Write7BitEncodedInt64(value.Ticks);
}
