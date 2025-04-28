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
        => sourceType switch
        {
            Type type when type == typeof(string) => true,
            Type type when type == typeof(IValue) => true,
            Type type when type == typeof(Binary) => true,
            _ => base.CanConvertFrom(context, sourceType),
        };

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value switch
        {
            string @string => ConvertFromString(@string, value.GetType()),
            Binary binary => ConvertFromBinary(binary, value.GetType()),
            _ => base.ConvertFrom(context, culture, value),
        };

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType switch
        {
            Type type when type == typeof(string) => true,
            Type type when type == typeof(IValue) => true,
            Type type when type == typeof(Binary) => true,
            _ => base.CanConvertTo(context, destinationType),
        };

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => destinationType switch
        {
            Type type when type == typeof(string) => value?.ToString(),
            Type type when type == typeof(IValue) => ConvertToBinary(value),
            Type type when type == typeof(Binary) => ConvertToBinary(value),
            _ => base.ConvertTo(context, culture, value, destinationType),
        };

    private static object ConvertFromString(string @string, Type type)
    {
        var methodInfo = GetParseMethod(type);
        return methodInfo.Invoke(null, [@string])!;
    }

    private static object ConvertFromBinary(Binary binary, Type type)
    {
        var bytes = binary.ToImmutableArray();
        return Activator.CreateInstance(type, [bytes])!;
    }

    private static object ConvertToBinary(object? value)
    {
        if (value is null)
        {
            return Null.Value;
        }

        var bytesProperty = GetBytesProperty(value.GetType());
        if (bytesProperty.GetValue(value) is ImmutableArray<byte> bytes)
        {
            return new Binary(bytes);
        }

        throw new UnreachableException(
            $"Failed to get {nameof(HashDigest<SHA1>.Bytes)} property value");
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
