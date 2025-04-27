using System.ComponentModel;
using System.Globalization;
using Bencodex.Types;

namespace Libplanet.Crypto.Converters;

internal sealed class PublicKeyTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string)
            || sourceType == typeof(IValue)
            || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string @string)
        {
            try
            {
                return PublicKey.Parse(@string);
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException)
            {
                throw new ArgumentException(e.Message, e);
            }
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

            if (destinationType == typeof(IValue))
            {
                return new Binary(publicKey.Bytes);
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
