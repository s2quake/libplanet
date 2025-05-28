using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class EvidenceIdModelConverter : ModelConverterBase<EvidenceId>
{
    protected override EvidenceId Deserialize(Stream stream, ModelOptions options)
    {
        var length = EvidenceId.Size;
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new EvidenceId(bytes.ToArray());
    }

    protected override void Serialize(EvidenceId obj, Stream stream, ModelOptions options)
        => stream.Write(obj.Bytes.AsSpan());
}
