using System.IO;

namespace Libplanet.Serialization.StaticConverters;

internal sealed class DateTimeOffsetModelConverter : ModelConverterBase<DateTimeOffset>
{
    protected override DateTimeOffset Read(BinaryReader reader, Type type, ModelOptions options)
        => new(reader.Read7BitEncodedInt64(), TimeSpan.Zero);

    protected override void Write(BinaryWriter writer, DateTimeOffset value, ModelOptions options)
        => writer.Write7BitEncodedInt64(value.UtcTicks);
}
