using System.Collections.Concurrent;
using System.Reflection;

namespace Libplanet.Serialization;

public sealed class LegacyModelResolver : IModelResolver
{
    private static readonly IModelResolver _resolver = ModelResolver.Default;
    private static readonly ConcurrentDictionary<Type, ImmutableArray<PropertyInfo>>
        _propertiesByType = [];

    private readonly Type _type;

    public LegacyModelResolver(Type type)
    {
        if (!type.IsDefined(typeof(ModelAttribute)))
        {
            throw new ArgumentException(
                $"Type does not have {nameof(ModelAttribute)}", nameof(type));
        }

        _type = type;
    }

    public ImmutableArray<PropertyInfo> GetProperties(Type type)
    {
        if (type == _type)
        {
            return _resolver.GetProperties(type);
        }

        var types = ModelResolver.GetTypes(_type);
        if (!types.Contains(type))
        {
            throw new ArgumentException("Type is not a valid type", nameof(type));
        }

        return _propertiesByType.GetOrAdd(type, ModelResolver.CreateProperties);
    }

    Type IModelResolver.GetType(Type type, int version)
        => _resolver.GetType(type, version);

    string IModelResolver.GetTypeName(Type type) => _resolver.GetTypeName(_type);

    int IModelResolver.GetVersion(Type type)
    {
        if (type == _type)
        {
            return _resolver.GetVersion(type);
        }

        var types = ModelResolver.GetTypes(_type);
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] == type)
            {
                return i + 1;
            }
        }

        throw new ArgumentException("Type is not a valid type", nameof(type));
    }
}
