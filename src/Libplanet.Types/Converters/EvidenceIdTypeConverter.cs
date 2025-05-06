using Bencodex.Types;
using Libplanet.Types.Evidence;

namespace Libplanet.Types.Converters;

internal sealed class EvidenceIdTypeConverter : TypeConverterBase<EvidenceId, Binary>
{
    protected override EvidenceId ConvertFromValue(Binary value) => new(value.ToByteArray());

    protected override Binary ConvertToValue(EvidenceId value) => new(value.Bytes);

    protected override EvidenceId ConvertFromString(string value) => EvidenceId.Parse(value);

    protected override string ConvertToString(EvidenceId value) => value.ToString();
}
