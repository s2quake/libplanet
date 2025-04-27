using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using Bencodex.Types;

namespace Libplanet.Common.Converters;

internal sealed class HashDigestTypeConverter : TypeConverter
{
    private static MethodInfo _fromString;

    public HashDigestTypeConverter(Type type)
    {
        if (!type.IsConstructedGenericType ||
            type.GetGenericTypeDefinition() != typeof(HashDigest<>) ||
            type.GetGenericArguments().Length != 1)
        {
            throw new ArgumentException(
                "Only usable with a constructed HashDigest<T>.",
                nameof(type));
        }

        _fromString = type.GetMethod(
            nameof(HashDigest<SHA1>.Parse),
            BindingFlags.Public | BindingFlags.Static,
            Type.DefaultBinder,
            new[] { typeof(string) },
            null
        ) ?? throw new MissingMethodException(
            $"Failed to look up the {nameof(HashDigest<SHA1>.Parse)} method");
    }

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
                return _fromString.Invoke(null, new[] { @string })!;
            }
            catch (TargetInvocationException e) when (e.InnerException is { } ie)
            {
                if (ie is ArgumentOutOfRangeException || ie is FormatException)
                {
                    throw new ArgumentException(ie.Message, ie);
                }

                throw ie;
            }
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
        if (value != null &&
            destinationType == typeof(string) &&
            value.GetType().IsConstructedGenericType &&
            value.GetType().GetGenericTypeDefinition() == typeof(HashDigest<>))
        {
            return value.ToString()!;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
