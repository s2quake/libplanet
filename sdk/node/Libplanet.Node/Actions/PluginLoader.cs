using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Libplanet.Node.Services;

namespace Libplanet.Node.Actions;

internal static class PluginLoader
{
    public static IActionProvider LoadActionProvider(string modulePath, string typeName)
    {
        if (Type.GetType(typeName) is { } type)
        {
            if (Activator.CreateInstance(type) is not IActionProvider obj)
            {
                throw new InvalidOperationException("type is not IActionProvider");
            }

            return obj;
        }

        var assembly = LoadAssembly(modulePath);
        return Create<IActionProvider>(assembly, typeName);
    }

    private static T Create<T>(Assembly assembly, string typeName)
        where T : class
    {
        if (assembly.GetType(typeName) is not { } type)
        {
            throw new ApplicationException(
                $"Can't find {typeName} in {assembly} from {assembly.Location}");
        }

        if (Activator.CreateInstance(type) is not T obj)
        {
            throw new ApplicationException(
                $"Can't create an instance of {type} in {assembly} from {assembly.Location}");
        }

        return obj;
    }

    private static bool TryGetLoadedAssembly(
        string modulePath, [MaybeNullWhen(false)] out Assembly assembly)
    {
        var loadedAssemblies = AssemblyLoadContext.All
            .SelectMany(context => context.Assemblies)
            .ToList();
        var comparison = StringComparison.OrdinalIgnoreCase;
        var comparer = new Predicate<Assembly>(
            assembly => string.Equals(assembly.Location, modulePath, comparison));

        if (loadedAssemblies.Find(comparer) is Assembly loadedAssembly)
        {
            assembly = loadedAssembly;
            return true;
        }

        assembly = null;
        return false;
    }

    private static Assembly LoadAssembly(string modulePath)
    {
        if (TryGetLoadedAssembly(modulePath, out var assembly))
        {
            return assembly;
        }

        var loadContext = new PluginLoadContext(modulePath);
        if (File.Exists(modulePath))
        {
            return loadContext.LoadFromAssemblyPath(modulePath);
        }

        if (Path.GetFileNameWithoutExtension(modulePath) is { } filename)
        {
            return loadContext.LoadFromAssemblyName(new AssemblyName(filename));
        }

        throw new ApplicationException($"Can't load plugin from {modulePath}");
    }
}
