using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class TxIdModelConverter : ModelConverterBase<TxId>
{
    protected override TxId Deserialize(BinaryReader reader, ModelOptions options)
        => new(reader.ReadBytes(TxId.Size));

    protected override void Serialize(TxId obj, BinaryWriter writer, ModelOptions options)
        => writer.Write(obj.Bytes.AsSpan());
}
