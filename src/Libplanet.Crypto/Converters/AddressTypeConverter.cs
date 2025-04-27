using System.ComponentModel;
using System.Globalization;
using Bencodex.Types;

namespace Libplanet.Crypto.Converters;

internal sealed class AddressTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string)
            || sourceType == typeof(IValue)
            || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(
        ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string v ? Address.Parse(v) : base.ConvertFrom(context, culture, value);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string)
            || destinationType == typeof(IValue)
            || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is Address address)
        {
            if (destinationType == typeof(string))
            {
                return (object?)$"{address:raw}";
            }

            if (destinationType == typeof(IValue))
            {
                return new Binary(address.Bytes);
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
