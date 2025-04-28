using Bencodex.Types;
using Libplanet.Common.Converters;

namespace Libplanet.Crypto.Converters;

internal sealed class AddressTypeConverter : TypeConverterBase<Address, Binary>
{
    protected override Address ConvertFromValue(Binary value) => new(value.ToByteArray());

    protected override Binary ConvertToValue(Address value) => new(value.Bytes);

    protected override Address ConvertFromString(string value) => Address.Parse(value);

    protected override string ConvertToString(Address value) => $"{value:raw}";
}
