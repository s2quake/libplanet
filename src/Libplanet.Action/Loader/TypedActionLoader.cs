using System.Reflection;
using Bencodex.Types;

namespace Libplanet.Action.Loader;

public class TypedActionLoader : IActionLoader
{
    private IDictionary<IValue, Type> _types;

    public TypedActionLoader(IDictionary<IValue, Type> types)
    {
        if (types.Count == 0)
        {
            throw new ArgumentException(
                $"Give {nameof(types)} cannot be empty.", nameof(types));
        }

        _types = types;
    }

    public IDictionary<IValue, Type> Types => _types;

    public static TypedActionLoader Create(Assembly? assembly = null, Type? baseType = null)
    {
        if (assembly is null && baseType is null)
        {
            throw new ArgumentException(
                $"At least one of {nameof(assembly)} and {nameof(baseType)} must be non-null.");
        }

        assembly = assembly ?? baseType!.Assembly;
        return new TypedActionLoader(LoadTypes(assembly, baseType));
    }

    public IAction LoadAction(IValue value)
    {
        try
        {
            // if (Registry.IsSystemAction(value))
            // {
            //     return Registry.Deserialize(value);
            // }

            IAction action;
            if (value is Dictionary pv &&
                pv.TryGetValue((Text)"type_id", out IValue rawTypeId) &&
                rawTypeId is IValue typeId &&
                Types.TryGetValue(typeId, out Type? actionType))
            {
                // NOTE: This is different from how PolymorphicAction<T> handles plain values.
                // Actual underlying types are expected to handle (or at least accept)
                // a plainvalue *with* "type_id" field included.
                action = (IAction)Activator.CreateInstance(actionType)!;
                // action.LoadPlainValue(pv);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Failed to instantiate an action from {value}");
            }

            return action;
        }
        catch (Exception e)
        {
            throw new InvalidActionException(
                $"Failed to instantiate an action from {value}", value, e);
        }
    }

    private static IDictionary<IValue, Type> LoadTypes(Assembly assembly, Type? baseType)
    {
        var types = new Dictionary<IValue, Type>();
        var actionType = typeof(IAction);
        foreach (Type type in LoadAllActionTypes(assembly))
        {
            if (baseType is { } bType && !bType.IsAssignableFrom(type))
            {
                continue;
            }

            if (type.GetCustomAttribute<ActionTypeAttribute>()?.TypeIdentifier is { } typeId)
            {
                if (types.TryGetValue(typeId, out Type? existing))
                {
                    if (existing != type)
                    {
                        throw new DuplicateActionTypeIdentifierException(
                            "Multiple action types are associated with the same type ID.",
                            typeId.ToString() ?? "null",
                            ImmutableHashSet.Create(existing, type));
                    }

                    continue;
                }

                types[typeId] = type;
            }
        }

        return types;
    }

    private static IEnumerable<Type> LoadAllActionTypes(Assembly assembly)
    {
        var actionType = typeof(IAction);
        foreach (Type t in assembly.GetTypes())
        {
            if (actionType.IsAssignableFrom(t))
            {
                yield return t;
            }
        }
    }
}
