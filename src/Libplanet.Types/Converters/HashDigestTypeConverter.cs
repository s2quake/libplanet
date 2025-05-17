using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;

namespace Libplanet.Types.Converters;

internal sealed class HashDigestTypeConverter(Type type) : TypeConverter
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> _parseMethodByType = [];

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType switch
        {
            Type type1 when type1 == typeof(string) => true,
            _ => base.CanConvertFrom(context, sourceType),
        };

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value switch
        {
            string @string => ConvertFromString(@string, type),
            _ => base.ConvertFrom(context, culture, value),
        };

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType switch
        {
            Type type1 when type1 == typeof(string) => true,
            _ => base.CanConvertTo(context, destinationType),
        };

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => destinationType switch
        {
            Type type1 when type1 == typeof(string) => value?.ToString(),
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
}
