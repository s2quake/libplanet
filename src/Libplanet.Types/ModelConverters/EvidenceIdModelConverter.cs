using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class EvidenceIdModelConverter : ModelConverterBase<EvidenceId>
{
    protected override EvidenceId Deserialize(BinaryReader reader, ModelOptions options)
        => new(reader.ReadBytes(EvidenceId.Size));

    protected override void Serialize(EvidenceId obj, BinaryWriter writer, ModelOptions options)
        => writer.Write(obj.Bytes.AsSpan());
}
