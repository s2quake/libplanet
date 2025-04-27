using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using Bencodex.Types;

namespace Libplanet.Common.Converters;

internal sealed class HashDigestTypeConverter : TypeConverter
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> _parseMethodByType = [];
    private static readonly ConcurrentDictionary<Type, PropertyInfo> _bytesPropertyByType = [];

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
                var methodInfo = GetParseMethod(value.GetType());
                return methodInfo.Invoke(null, [@string])!;
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
        if (value != null
            && value.GetType().IsConstructedGenericType
            && value.GetType().GetGenericTypeDefinition() == typeof(HashDigest<>))
        {
            if (destinationType == typeof(string))
            {
                return value.ToString();
            }
            else if (destinationType == typeof(IValue))
            {
                var bytesProperty = GetBytesProperty(value.GetType());
                if (bytesProperty.GetValue(value) is ImmutableArray<byte> bytes)
                {
                    return new Binary(bytes);
                }

                throw new UnreachableException(
                    $"Failed to get {nameof(HashDigest<SHA1>.Bytes)} property value");
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    private static MethodInfo GetParseMethod(Type type)
        => _parseMethodByType.GetOrAdd(type, GetMethodInfo);

    private static MethodInfo GetMethodInfo(Type type)
    {
        var methodName = nameof(HashDigest<SHA1>.Parse);
        var bindingFlags = BindingFlags.Public | BindingFlags.Static;
        var method = type.GetMethod(methodName, bindingFlags, [typeof(string)])
            ?? throw new MissingMethodException(
                $"Failed to look up the {nameof(HashDigest<SHA1>.Parse)} method");
        return method;
    }

    private static PropertyInfo GetBytesProperty(Type type)
        => _bytesPropertyByType.GetOrAdd(type, GetPropertyInfo);

    private static PropertyInfo GetPropertyInfo(Type type)
    {
        var propertyName = nameof(HashDigest<SHA1>.Bytes);
        var bindingFlags = BindingFlags.Public | BindingFlags.Instance;
        var property = type.GetProperty(propertyName, bindingFlags)
            ?? throw new MissingMethodException(
                $"Failed to look up the {nameof(HashDigest<SHA1>.Bytes)} property");
        return property;
    }
}
