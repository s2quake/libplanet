using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using Bencodex.Types;

namespace Libplanet.Types.Converters;

internal sealed class HashDigestTypeConverter(Type type) : TypeConverter
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> _parseMethodByType = [];
    private static readonly ConcurrentDictionary<Type, PropertyInfo> _bytesPropertyByType = [];

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType switch
        {
            Type type1 when type1 == typeof(string) => true,
            Type type2 when type2 == typeof(IValue) => true,
            Type type3 when type3 == typeof(Binary) => true,
            _ => base.CanConvertFrom(context, sourceType),
        };

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value switch
        {
            string @string => ConvertFromString(@string, type),
            Binary binary => ConvertFromBinary(binary, type),
            _ => base.ConvertFrom(context, culture, value),
        };

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType switch
        {
            Type type1 when type1 == typeof(string) => true,
            Type type2 when type2 == typeof(IValue) => true,
            Type type3 when type3 == typeof(Binary) => true,
            _ => base.CanConvertTo(context, destinationType),
        };

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => destinationType switch
        {
            Type type1 when type1 == typeof(string) => value?.ToString(),
            Type type2 when type2 == typeof(IValue) => ConvertToBinary(value),
            Type type3 when type3 == typeof(Binary) => ConvertToBinary(value),
            _ => base.ConvertTo(context, culture, value, destinationType),
        };

    private static object ConvertFromString(string @string, Type type)
    {
        var methodInfo = GetParseMethod(type);
        try
        {
            return methodInfo.Invoke(null, [@string])!;
        }
        catch (TargetInvocationException e) when (e.InnerException is FormatException fe)
        {
            throw fe;
        }
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
