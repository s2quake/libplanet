using Libplanet.Types.Evidence;

namespace Libplanet.Types.Converters;

internal sealed class EvidenceIdTypeConverter : TypeConverterBase<EvidenceId>
{
    protected override EvidenceId ConvertFromString(string value) => EvidenceId.Parse(value);

    protected override string ConvertToString(EvidenceId value) => value.ToString();
}
