using Libplanet.Types.Tx;

namespace Libplanet.Types.Converters;

internal sealed class TxIdTypeConverter : TypeConverterBase<TxId>
{
    protected override TxId ConvertFromValue(byte[] value) => new(value);

    protected override byte[] ConvertToValue(TxId value) => [..value.Bytes];

    protected override TxId ConvertFromString(string value) => TxId.Parse(value);

    protected override string ConvertToString(TxId value) => value.ToString();
}
