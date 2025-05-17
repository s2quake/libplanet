using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class HashDigestModelConverter(Type type) : ModelConverterBase
{
    // private static readonly ConcurrentDictionary<Type, MethodInfo> _parseMethodByType = [];
    private static readonly ConcurrentDictionary<Type, PropertyInfo> _bytesPropertyByType = [];
    private static readonly ConcurrentDictionary<Type, int> _sizeByType = [];

    // public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    //     => sourceType switch
    //     {
    //         Type type1 when type1 == typeof(string) => true,
    //         Type type2 when type2 == typeof(byte[]) => true,
    //         _ => base.CanConvertFrom(context, sourceType),
    //     };

    // public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    //     => value switch
    //     {
    //         string @string => ConvertFromString(@string, type),
    //         byte[] binary => ConvertFromBinary(binary, type),
    //         _ => base.ConvertFrom(context, culture, value),
    //     };

    // public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    //     => destinationType switch
    //     {
    //         Type type1 when type1 == typeof(string) => true,
    //         Type type2 when type2 == typeof(byte[]) => true,
    //         _ => base.CanConvertTo(context, destinationType),
    //     };

    // public override object? ConvertTo(
    //     ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    //     => destinationType switch
    //     {
    //         Type type1 when type1 == typeof(string) => value?.ToString(),
    //         Type type2 when type2 == typeof(byte[]) => ConvertToBinary(value),
    //         _ => base.ConvertTo(context, culture, value, destinationType),
    //     };

    // private static object ConvertFromString(string @string, Type type)
    // {
    //     var methodInfo = GetParseMethod(type);
    //     try
    //     {
    //         return methodInfo.Invoke(null, [@string])!;
    //     }
    //     catch (TargetInvocationException e) when (e.InnerException is FormatException fe)
    //     {
    //         throw fe;
    //     }
    // }

    // private static object ConvertFromBinary(byte[] binary, Type type)
    // {
    //     var bytes = binary.ToImmutableArray();
    //     return Activator.CreateInstance(type, [bytes])!;
    // }

    // private static object ConvertToBinary(object? value)
    // {
    //     if (value is null)
    //     {
    //         return new byte[] { 0 };
    //     }

    //     var bytesProperty = GetBytesProperty(value.GetType());
    //     if (bytesProperty.GetValue(value) is ImmutableArray<byte> bytes)
    //     {
    //         return bytes.ToArray();
    //     }

    //     throw new UnreachableException(
    //         $"Failed to get {nameof(HashDigest<SHA1>.Bytes)} property value");
    // }

    // private static MethodInfo GetParseMethod(Type type)
    //     => _parseMethodByType.GetOrAdd(type, GetMethodInfo);

    // private static MethodInfo GetMethodInfo(Type type)
    // {
    //     var methodName = nameof(HashDigest<SHA1>.Parse);
    //     var bindingFlags = BindingFlags.Public | BindingFlags.Static;
    //     var method = type.GetMethod(methodName, bindingFlags, [typeof(string)])
    //         ?? throw new MissingMethodException(
    //             $"Failed to look up the {nameof(HashDigest<SHA1>.Parse)} method");
    //     return method;
    // }

    private static PropertyInfo GetBytesProperty(Type type)
    {
        return _bytesPropertyByType.GetOrAdd(type, GetPropertyInfo);

        static PropertyInfo GetPropertyInfo(Type type)
        {
            var propertyName = nameof(HashDigest<SHA1>.Bytes);
            var bindingFlags = BindingFlags.Public | BindingFlags.Instance;
            var property = type.GetProperty(propertyName, bindingFlags)
                ?? throw new MissingMethodException(
                    $"Failed to look up the {nameof(HashDigest<SHA1>.Bytes)} property");
            return property;
        }
    }

    private static int GetSize(Type type)
    {
        return _sizeByType.GetOrAdd(type, GetField);

        static int GetField(Type type)
        {
            var fieldName = nameof(HashDigest<SHA1>.Size);
            var bindingFlags = BindingFlags.Public | BindingFlags.Static;
            var field = type.GetProperty(fieldName, bindingFlags)
                ?? throw new UnreachableException("Could not find the field");
            if (field.GetValue(null) is not int size)
            {
                throw new UnreachableException(
                    $"Failed to get {nameof(HashDigest<SHA1>.Size)} field value");
            }

            return size;
        }
    }

    protected override object Deserialize(Stream stream)
    {
        var length = GetSize(type);
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        if (Activator.CreateInstance(type, [bytes.ToImmutableArray()]) is not object obj)
        {
            throw new UnreachableException(
                $"Failed to create an instance of {type.Name}");
        }

        return obj;
    }

    protected override void Serialize(object obj, Stream stream)
    {
        var bytesProperty = GetBytesProperty(type);
        if (bytesProperty.GetValue(obj) is ImmutableArray<byte> bytes)
        {
            stream.Write(bytes.AsSpan());
        }
        else
        {
            throw new UnreachableException(
                $"Failed to get {nameof(HashDigest<SHA1>.Bytes)} property value");
        }
    }
}
