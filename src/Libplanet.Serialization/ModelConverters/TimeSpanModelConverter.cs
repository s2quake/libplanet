using System.IO;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class TimeSpanModelConverter : ModelConverterBase<TimeSpan>
{
    protected override TimeSpan Deserialize(Stream stream, ModelOptions options)
    {
        var length = sizeof(long);
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new TimeSpan(BitConverter.ToInt64(bytes));
    }

    protected override void Serialize(TimeSpan obj, Stream stream, ModelOptions options)
    {
        var bytes = BitConverter.GetBytes(obj.Ticks);
        stream.Write(bytes, 0, bytes.Length);
    }
}
