using Bencodex.Types;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Converters;

internal sealed class TxIdTypeConverter : TypeConverterBase<TxId, Binary>
{
    protected override TxId ConvertFromValue(Binary value) => new(value.ToByteArray());

    protected override Binary ConvertToValue(TxId value) => new(value.Bytes);

    protected override TxId ConvertFromString(string value) => TxId.Parse(value);

    protected override string ConvertToString(TxId value) => value.ToString();
}
