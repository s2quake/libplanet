using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Serialization;

internal sealed class TypeResolver
{
    private readonly ConcurrentDictionary<string, Type> _typeByName = [];
    private readonly ConcurrentDictionary<Type, string> _nameByType = [];

    public void AddType(Type type, string typeName)
    {
        if (_typeByName.ContainsKey(typeName))
        {
            throw new ArgumentException($"Type name '{typeName}' is already registered.", nameof(typeName));
        }

        if (_nameByType.ContainsKey(type))
        {
            throw new ArgumentException($"Type '{type.FullName}' is already registered.", nameof(type));
        }

        _typeByName.TryAdd(typeName, type);
        _nameByType.TryAdd(type, typeName);
    }

    public Type GetType(string typeName) => _typeByName[typeName];

    public bool TryGetType(string typeName, [MaybeNullWhen(false)] out Type type)
        => _typeByName.TryGetValue(typeName, out type);

    public string GetTypeName(Type type) => _nameByType[type];

    public bool TryGetTypeName(Type type, [MaybeNullWhen(false)] out string typeName)
        => _nameByType.TryGetValue(type, out typeName);
}
