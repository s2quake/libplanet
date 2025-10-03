using System.Diagnostics;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class KeyValuePairYamlTypeConverter : YamlTypeConverter<object>
{
    private static readonly string[] _names =
    [
        nameof(KeyValuePair<object, object>.Key),
        nameof(KeyValuePair<object, object>.Value),
    ];

    public override bool Accepts(Type type) => IsKeyValuePair(type);

    public override object? Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var values = new object?[_names.Length];
        parser.ReadStartObject();
        for (var i = 0; i < _names.Length; i++)
        {
            if (type.GetProperty(_names[i]) is not { } property)
            {
                throw new UnreachableException($"{_names[i]} property not found");
            }

            var propertyType = property.PropertyType;
            using var _ = ModelTypeScope.Push(propertyType);
            values[i] = rootDeserializer(propertyType);
        }

        parser.ReadEndObject();
        return TypeUtility.CreateInstance(type, args: values);
    }

    public override void Write(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
    {
        emitter.WriteStartObject();
        for (var i = 0; i < _names.Length; i++)
        {
            if (type.GetProperty(_names[i]) is not { } property)
            {
                throw new UnreachableException($"{_names[i]} property not found");
            }

            var propertyType = property.PropertyType;
            var propertyValue = property.GetValue(value);
            var propertyActualType = TypeUtility.GetActualType(propertyValue, propertyType);
            using var _ = ModelTypeScope.Push(propertyType);
            serializer(propertyValue, propertyActualType);
        }

        emitter.WriteEndObject();
    }

    private static bool IsKeyValuePair(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        return genericTypeDefinition == typeof(KeyValuePair<,>);
    }
}
