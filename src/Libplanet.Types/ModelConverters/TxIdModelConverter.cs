using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class TxIdModelConverter : ModelConverterBase<TxId>
{
    protected override void Serialize(TxId obj, ref ModelWriter writer, ModelOptions options)
        => writer.Write(obj.Bytes.AsSpan());

    protected override TxId Deserialize(ref ModelReader reader, ModelOptions options)
        => new(reader.ReadBytes());
}
