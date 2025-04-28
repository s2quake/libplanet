using System.ComponentModel;
using System.Globalization;
using Bencodex.Types;

namespace Libplanet.Crypto.Converters;

internal sealed class PublicKeyTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string)
            || sourceType == typeof(IValue)
            || sourceType == typeof(Binary)
            || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string @string)
        {
            return PublicKey.Parse(@string);
        }
        else if (value is Binary binary)
        {
            return new PublicKey([.. binary.ToByteArray()]);
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string)
            || destinationType == typeof(IValue)
            || destinationType == typeof(Binary)
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

            if (destinationType == typeof(IValue) || destinationType == typeof(Binary))
            {
                return new Binary(publicKey.Bytes);
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
