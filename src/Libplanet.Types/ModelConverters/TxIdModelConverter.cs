using System.IO;
using Libplanet.Serialization;
using Libplanet.Types.Transactions;

namespace Libplanet.Types.ModelConverters;

internal sealed class TxIdModelConverter : ModelConverterBase<TxId>
{
    protected override TxId Deserialize(Stream stream, ModelOptions options)
    {
        var length = TxId.Size;
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new TxId(bytes.ToArray());
    }

    protected override void Serialize(TxId obj, Stream stream, ModelOptions options)
        => stream.Write(obj.Bytes.AsSpan());
}
