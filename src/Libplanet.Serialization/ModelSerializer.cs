using System.IO;
using System.Text;
using static Libplanet.Serialization.ModelResolver;

namespace Libplanet.Serialization;

public static class ModelSerializer
{
    private enum DataType : byte
    {
        Null,

        Default,

        Value,

        Header,
    }

    public static byte[] Serialize<T>(T? obj)
        where T : notnull
        => Serialize(obj, ModelOptions.Empty);

    public static byte[] Serialize<T>(T? obj, ModelOptions options)
        where T : notnull
        => Serialize(obj, obj?.GetType() ?? typeof(T), options);

    public static byte[] Serialize(object? obj, Type type) => Serialize(obj, type, ModelOptions.Empty);

    public static byte[] Serialize(object? obj, Type type, ModelOptions options)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        Serialize(writer, obj, type, options);
        return stream.ToArray();
    }

    public static void Serialize(BinaryWriter writer, object? value, Type type, ModelOptions options)
    {
        if (value is null)
        {
            writer.Write((byte)DataType.Null);
            return;
        }

        if (!ModelTypeScope.CanOmitTypeInfo(type, options))
        {
            var data = new ModelData(type);
            writer.Write((byte)DataType.Header);
            data.Write(writer);
        }

        if (TypeUtility.IsDefault(value, type))
        {
            writer.Write((byte)DataType.Default);
        }
        else if (TryGetConverter(type, out var converter))
        {
            writer.Write((byte)DataType.Value);
            SerializeByConverter(writer, value, options, converter);
        }
        else
        {
            throw new ModelException($"Unsupported type {type}");
        }
    }

    public static object? Deserialize(ReadOnlySpan<byte> bytes) => Deserialize(bytes, ModelOptions.Empty);

    public static object? Deserialize(ReadOnlySpan<byte> bytes, ModelOptions options)
        => Deserialize(bytes, typeof(object), options);

    public static object? Deserialize(ReadOnlySpan<byte> bytes, Type type)
        => Deserialize(bytes, type, ModelOptions.Empty);

    public static object? Deserialize(ReadOnlySpan<byte> bytes, Type type, ModelOptions options)
    {
        using var stream = new MemoryStream(bytes.ToArray());
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return Deserialize(reader, type, options);
    }

    public static object? Deserialize(BinaryReader reader, Type type, ModelOptions options)
    {
        var dataType = (DataType)reader.Read();
        if (dataType == DataType.Null)
        {
            return null;
        }

        if (ModelTypeScope.CanOmitTypeInfo(type) && dataType == DataType.Header)
        {
            throw new ModelException("Type information is required but not found.");
        }

        var actualType = type;
        if (dataType == DataType.Header)
        {
            var header = ModelData.GetData(reader);
            var headerType = TypeUtility.GetType(header.TypeName);
            actualType = ModelResolver.GetType(headerType, header.Version);
            dataType = (DataType)reader.Read();
        }

        if (actualType == typeof(object))
        {
            throw new ModelException("Type information is not found. Specify the exact type explicitly.");
        }

        if (dataType == DataType.Default)
        {
            return TypeUtility.GetDefault(actualType);
        }
        else if (dataType == DataType.Value && TryGetConverter(actualType, out var converter))
        {
            return converter.Read(reader, actualType, options);
        }

        throw new ModelException($"Invalid data type {actualType}.");
    }

    public static T Deserialize<T>(BinaryReader reader, ModelOptions options)
        where T : notnull
    {
        if (Deserialize(reader, typeof(T), options) is T obj)
        {
            return obj;
        }

        throw new ModelException($"Failed to deserialize {typeof(T)}.");
    }

    public static T Deserialize<T>(ReadOnlySpan<byte> bytes)
        where T : notnull
        => Deserialize<T>(bytes, ModelOptions.Empty);

    public static T Deserialize<T>(ReadOnlySpan<byte> bytes, ModelOptions options)
        where T : notnull
    {
        using var stream = new MemoryStream(bytes.ToArray());
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return Deserialize<T>(reader, options);
    }

    public static T Clone<T>(T obj) where T : notnull => Clone(obj, ModelOptions.Empty);

    public static T Clone<T>(T obj, ModelOptions options)
        where T : notnull
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        Serialize(writer, obj, typeof(T), options);
        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return Deserialize<T>(reader, options);
    }

    private static void SerializeByConverter(
        BinaryWriter writer, object obj, ModelOptions options, IModelConverter converter)
    {
        try
        {
            converter.Write(writer, obj, options);
        }
        catch (Exception e) when (e is not ModelException)
        {
            var message = $"An exception occurred while serializing {obj.GetType()} by {converter.GetType()}.";
            throw new ModelException(message, e);
        }
    }
}
