using System.Reflection;

namespace Libplanet.Serialization.Tests;

public static class ModelTestUtility
{
    public static void AssertProperty<T>(string propertyName, int index)
    {
        var property = typeof(T).GetProperty(propertyName);
        Assert.NotNull(property);
        var attribute = property.GetCustomAttribute<PropertyAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(index, attribute.Index);
    }
}
