#pragma warning disable S1066 // Collapsible "if" statements should be merged
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Libplanet.Types.Converters;

public abstract class TypeConverterBase<TType> : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        if (sourceType == typeof(string))
        {
            return true;
        }

        if (sourceType == typeof(byte[]))
        {
            return true;
        }

        return base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string @string)
        {
            return ConvertFromString(@string);
        }

        if (value is byte[] typeValue)
        {
            return ConvertFromValue(typeValue);
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destinationType)
    {
        if (destinationType == typeof(string))
        {
            return true;
        }

        if (destinationType == typeof(byte[]))
        {
            return true;
        }

        return base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string))
        {
            if (value is TType typeValue)
            {
                return ConvertToString(typeValue);
            }
        }

        if (destinationType == typeof(byte[]))
        {
            if (value is null)
            {
                return new byte[] { 0 };
            }

            if (value is TType typeValue)
            {
                return ConvertToValue(typeValue);
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    protected abstract TType ConvertFromValue(byte[] value);

    protected abstract byte[] ConvertToValue(TType value);

    protected abstract new TType ConvertFromString(string value);

    protected abstract string ConvertToString(TType value);
}
