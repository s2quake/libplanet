using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Bencodex.Types;

namespace Libplanet.Serialization.Converters;

internal abstract class InternalTypeConverterBase<TType, TValueType> : TypeConverter
    where TValueType : IValue
{
    private static readonly TypeConverter _defaultConverter = TypeDescriptor.GetConverter(typeof(TType));

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        if (sourceType == typeof(TValueType))
        {
            return true;
        }

        return _defaultConverter.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is TValueType typeValue)
        {
            return ConvertFromValue(typeValue);
        }

        return _defaultConverter.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destinationType)
    {
        if (destinationType == typeof(TValueType) || destinationType == typeof(IValue))
        {
            return true;
        }

        return _defaultConverter.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(TValueType) || destinationType == typeof(IValue))
        {
            if (value is null)
            {
                return Null.Value;
            }

            if (value is TType typeValue)
            {
                return ConvertToValue(typeValue);
            }
        }

        return _defaultConverter.ConvertTo(context, culture, value, destinationType);
    }

    protected abstract TType ConvertFromValue(TValueType value);

    protected abstract TValueType ConvertToValue(TType value);
}
