using System;
using System.ComponentModel;
using System.Globalization;

namespace Libplanet.Crypto.Converters;

internal sealed class AddressTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(
        ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string v ? Address.Parse(v) : base.ConvertFrom(context, culture, value);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
        => value is Address address && destinationType == typeof(string)
            ? $"{address:raw}"
            : base.ConvertTo(context, culture, value, destinationType);
}
