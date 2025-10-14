using System.Collections.Concurrent;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml;

public static class ModelYamlSerializer
{
    private static readonly ConcurrentDictionary<ModelOptions, ISerializer> _serializerByOptions = [];
    private static readonly ConcurrentDictionary<ModelOptions, IDeserializer> _deserializerByOptions = [];

    public static string Serialize<T>(T? obj)
        where T : notnull
        => Serialize(obj, ModelOptions.Empty);

    public static string Serialize<T>(T? obj, ModelOptions options)
        where T : notnull
        => Serialize(obj, obj?.GetType() ?? typeof(T), options);

    public static string Serialize(object? obj, Type type) => Serialize(obj, type, ModelOptions.Empty);

    public static string Serialize(object? obj, Type type, ModelOptions options)
    {
        using var _ = ModelOptionsScope.Push(options);
        try
        {
            var serializer = GetSerializer(options);
            return serializer.Serialize(obj, type);
        }
        catch (Exception e) when (e is not ModelException)
        {
            throw new ModelException("Failed to serialize to string.", e);
        }
    }

    public static T Deserialize<T>(string json)
        where T : notnull
        => Deserialize<T>(json, ModelOptions.Empty);

    public static T Deserialize<T>(string json, ModelOptions options)
        where T : notnull
    {
        var obj = Deserialize(json, typeof(T), options);

        if (obj is not T t)
        {
            throw new ModelException("Failed to deserialize from string.");
        }

        return t;
    }

    public static object? Deserialize(string json) => Deserialize(json, ModelOptions.Empty);

    public static object? Deserialize(string json, ModelOptions options) => Deserialize(json, typeof(object), options);

    public static object? Deserialize(string json, Type type)
        => Deserialize(json, type, ModelOptions.Empty);

    public static object? Deserialize(string json, Type type, ModelOptions options)
    {
        using var _ = ModelOptionsScope.Push(options);
        try
        {
            var deserializer = GetDeserializer(options);
            return deserializer.Deserialize(json, type);
        }
        catch (YamlException e)
        {
            throw new ModelException("Failed to deserialize from string.", e);
        }
    }

    public static T Clone<T>(T obj) where T : notnull => Clone(obj, ModelOptions.Empty);

    public static T Clone<T>(T obj, ModelOptions options)
        where T : notnull
    {
        var serialized = Serialize(obj, typeof(T), options);
        return Deserialize<T>(serialized, options);
    }

    private static ISerializer GetSerializer(ModelOptions options)
        => _serializerByOptions.GetOrAdd(options, CreateSerializer);

    private static ISerializer CreateSerializer(ModelOptions options)
        => new SerializerBuilder()
            .WithTypeConverter(new ModelYamlTypeConverter())
            .WithIndentedSequences()
            .Build();

    private static IDeserializer GetDeserializer(ModelOptions options)
        => _deserializerByOptions.GetOrAdd(options, CreateDeserializer);

    private static IDeserializer CreateDeserializer(ModelOptions options)
        => new DeserializerBuilder()
            .WithTypeConverter(new ModelYamlTypeConverter())
            .WithNodeTypeResolver(new NodeTypeResolver())
            .Build();
}
