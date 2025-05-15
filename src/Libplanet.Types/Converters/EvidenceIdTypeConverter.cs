using Libplanet.Types.Evidence;

namespace Libplanet.Types.Converters;

internal sealed class EvidenceIdTypeConverter : TypeConverterBase<EvidenceId>
{
    protected override EvidenceId ConvertFromValue(byte[] value) => new(value);

    protected override byte[] ConvertToValue(EvidenceId value) => [.. value.Bytes];

    protected override EvidenceId ConvertFromString(string value) => EvidenceId.Parse(value);

    protected override string ConvertToString(EvidenceId value) => value.ToString();
}
