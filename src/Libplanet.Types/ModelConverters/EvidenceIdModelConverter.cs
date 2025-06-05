using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class EvidenceIdModelConverter : ModelConverterBase<EvidenceId>
{
    protected override void Serialize(EvidenceId obj, ref ModelWriter writer, ModelOptions options)
        => writer.Write(obj.Bytes.AsSpan());

    protected override EvidenceId Deserialize(ref ModelReader reader, ModelOptions options)
        => new(reader.ReadBytes());
}
