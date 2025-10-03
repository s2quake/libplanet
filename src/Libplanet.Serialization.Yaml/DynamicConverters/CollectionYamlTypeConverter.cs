using System.Collections;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal abstract class CollectionYamlTypeConverter : YamlTypeConverter<IEnumerable>
{
    protected abstract Type GenericTypeDefinition { get; }

    protected abstract bool IsDictionary { get; }

    public override IEnumerable Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        parser.ReadStartArray();
        var elementType = GetElementType(type);
        var listInstance = CreateListInstance(elementType);
        using var _ = ModelTypeScope.Push(elementType);
        while (parser.Current?.GetType() != typeof(SequenceEnd))
        {
            var value = rootDeserializer(elementType);
            listInstance.Add(value);
        }

        parser.ReadEndArray();
        return CreateInstance(type, elementType, listInstance);
        throw new NotImplementedException();
    }

    public override void Write(IEmitter emitter, IEnumerable value, Type type, ObjectSerializer serializer)
    {
        emitter.WriteStartArray();
        var enumerator = value.GetEnumerator();
        var elementType = GetElementType(value.GetType());
        using var _ = ModelTypeScope.Push(elementType);
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            var currentType = TypeUtility.GetActualType(current, elementType);
            serializer(current, currentType);
        }

        emitter.WriteEndArray();
    }

    public override bool Accepts(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == GenericTypeDefinition;

    protected virtual IEnumerable CreateInstance(Type typeToConvert, Type elementType, IList listInstance)
        => (IEnumerable)TypeUtility.CreateInstance(typeToConvert, args: [listInstance]);

    protected virtual Type GetElementType(Type typeToConvert)
    {
        if (Accepts(typeToConvert))
        {
            if (IsDictionary)
            {
                return typeof(KeyValuePair<,>).MakeGenericType(typeToConvert.GetGenericArguments());
            }

            return typeToConvert.GetGenericArguments()[0];
        }

        throw new NotSupportedException("The type is not supported.");
    }

    private static IList CreateListInstance(Type elementType)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        return (IList)TypeUtility.CreateInstance(listType, args: [])!;
    }
}
