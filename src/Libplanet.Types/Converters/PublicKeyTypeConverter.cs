using System.ComponentModel;
using System.Globalization;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Converters;

internal sealed class PublicKeyTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string)
            || sourceType == typeof(byte[])
            || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string @string)
        {
            return PublicKey.Parse(@string);
        }
        else if (value is byte[] binary)
        {
            return new PublicKey([.. binary], verify: false);
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string)
            || destinationType == typeof(byte[])
            || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is PublicKey publicKey)
        {
            if (destinationType == typeof(string))
            {
                return (object?)publicKey.ToString("c", null);
            }

            if (destinationType == typeof(byte[]))
            {
                return publicKey.Bytes.ToArray();
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
