using System.Reflection;

namespace Libplanet.Serialization.Json.Schema;

internal static class ServiceUtility
{
    public static IEnumerable<Type> GetTypes(Assembly assembly, Type attributeType, bool inherit)
    {
        try
        {
            return assembly.GetTypes()
            .Where(type => Attribute.IsDefined(type, attributeType, inherit))
            .Where(type => type.IsClass && !type.IsAbstract);
        }
        catch (ReflectionTypeLoadException)
        {
            return [];
        }
    }

    public static IEnumerable<Type> GetTypes(Type attributeType, bool inherit)
        => AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => GetTypes(assembly, attributeType, inherit));
}
