using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class HashDigestModelConverter(Type type) : ModelConverterBase(type)
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo> _bytesPropertyByType = [];
    private static readonly ConcurrentDictionary<Type, int> _sizeByType = [];

    protected override object Deserialize(Stream stream, ModelOptions options)
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

    protected override void Serialize(object obj, Stream stream, ModelOptions options)
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
            var field = type.GetField(fieldName, bindingFlags)
                ?? throw new UnreachableException("Could not find the field");
            if (field.GetValue(null) is not int size)
            {
                throw new UnreachableException(
                    $"Failed to get {nameof(HashDigest<SHA1>.Size)} field value");
            }

            return size;
        }
    }
}
