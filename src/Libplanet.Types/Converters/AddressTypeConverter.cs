using Libplanet.Types;

namespace Libplanet.Types.Converters;

internal sealed class AddressTypeConverter : TypeConverterBase<Address>
{
    protected override Address ConvertFromString(string value) => Address.Parse(value);

    protected override string ConvertToString(Address value) => $"{value:raw}";
}
