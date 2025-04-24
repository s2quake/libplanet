using System.ComponentModel;
using System.Globalization;

namespace Libplanet.Crypto.Converters;

internal sealed class PublicKeyTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(
        ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string v)
        {
            try
            {
                return PublicKey.Parse(v);
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException)
            {
                throw new ArgumentException(e.Message, e);
            }
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
        => value is PublicKey key && destinationType == typeof(string)
            ? key.ToString("c", null)
            : base.ConvertTo(context, culture, value, destinationType);
}
