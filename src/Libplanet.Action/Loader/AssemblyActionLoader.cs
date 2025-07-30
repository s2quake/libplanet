using System.Reflection;

namespace Libplanet.Action.Loader;

public sealed class AssemblyActionLoader : IActionLoader
{
    private readonly Dictionary<byte[], Type> _types = [];

    public AssemblyActionLoader(Assembly assembly)
    {
        // var query = from type in assembly.GetTypes()
        //             where typeof(IAction).IsAssignableFrom(type) == true
        //             let attribute = type.GetCustomAttribute<ActionTypeAttribute>()
        //             where attribute is not null
        //             select (attribute.TypeIdentifier, type);

        // _types = query.ToDictionary((item) => item.TypeIdentifier, item => item.type);
    }

    public IReadOnlyDictionary<byte[], Type> Types => _types;

    public IAction LoadAction(byte[] value)
    {
        throw new NotImplementedException();
        // try
        // {
        //     IAction action;
        //     if (value is Dictionary pv &&
        //         pv.TryGetValue((Text)"type_id", out var rawTypeId) &&
        //         rawTypeId is byte[] typeId &&
        //         Types.TryGetValue(typeId, out var actionType))
        //     {
        //         action = (IAction)Activator.CreateInstance(actionType)!;
        //         // action.LoadPlainValue(pv);
        //     }
        //     else
        //     {
        //         throw new InvalidOperationException(
        //             $"Failed to instantiate an action from {value}");
        //     }

        //     return action;
        // }
        // catch (Exception e)
        // {
        //     throw new InvalidOperationException($"Failed to instantiate an action from {value}", e);
        // }
    }
}
