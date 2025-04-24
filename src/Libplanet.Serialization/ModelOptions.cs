using System.Reflection;
using static Libplanet.Serialization.TypeUtility;

namespace Libplanet.Serialization;

public sealed record class ModelOptions
{
    public static readonly ModelOptions Default = new();

    public IModelResolver Resolver { get; init; } = ModelResolver.Default;

    internal string GetTypeName(Type type)
    {
        try
        {
            return Resolver.GetTypeName(type);
        }
        catch (Exception e)
        {
            throw new ModelSerializationException($"Failed to get type name for {type}", e);
        }
    }

    internal int GetVersion(Type type)
    {
        try
        {
            return IsStandardType(type) ? 0 : Resolver.GetVersion(type);
        }
        catch (Exception e)
        {
            throw new ModelSerializationException($"Failed to get version for {type}", e);
        }
    }

    internal ImmutableArray<PropertyInfo> GetProperties(Type type)
    {
        try
        {
            return Resolver.GetProperties(type);
        }
        catch (Exception e)
        {
            throw new ModelSerializationException($"Failed to get properties for {type}", e);
        }
    }

    internal Type GetType(Type type, int version)
    {
        try
        {
            return Resolver.GetType(type, version);
        }
        catch (Exception e)
        {
            throw new ModelSerializationException(
                $"Failed to get type for {type} with version {version}", e);
        }
    }
}
