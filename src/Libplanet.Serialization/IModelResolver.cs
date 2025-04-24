using System.Collections.Immutable;
using System.Reflection;

namespace Libplanet.Serialization;

public interface IModelResolver
{
    Type GetType(Type type, int version);

    string GetTypeName(Type type);

    int GetVersion(Type type);

    ImmutableArray<PropertyInfo> GetProperties(Type type);
}
