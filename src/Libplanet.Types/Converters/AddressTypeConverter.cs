using Libplanet.Types.Crypto;

namespace Libplanet.Types.Converters;

internal sealed class AddressTypeConverter : TypeConverterBase<Address>
{
    protected override Address ConvertFromValue(byte[] value) => new(value);

    protected override byte[] ConvertToValue(Address value) => [.. value.Bytes];

    protected override Address ConvertFromString(string value) => Address.Parse(value);

    protected override string ConvertToString(Address value) => $"{value:raw}";
}
