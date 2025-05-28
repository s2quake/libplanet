using System.ComponentModel;
using System.Globalization;
using Libplanet.Types;

namespace Libplanet.Types.Converters;

internal sealed class PublicKeyTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string)
            || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string @string)
        {
            return PublicKey.Parse(@string);
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string)
            || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is PublicKey publicKey && destinationType == typeof(string))
        {
            return (object?)publicKey.ToString("c", null);
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
