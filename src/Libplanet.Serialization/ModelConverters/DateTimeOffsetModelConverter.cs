using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class DateTimeOffsetModelConverter : ModelConverterBase<DateTimeOffset>
{
    protected override DateTimeOffset Deserialize(Stream stream)
    {
        var length = sizeof(long);
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new DateTimeOffset(BitConverter.ToInt64(bytes), TimeSpan.Zero);
    }

    protected override void Serialize(DateTimeOffset obj, Stream stream)
    {
        var bytes = BitConverter.GetBytes(obj.UtcTicks);
        stream.Write(bytes, 0, bytes.Length);
    }
}
