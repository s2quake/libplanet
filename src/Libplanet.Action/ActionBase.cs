using System.Collections.Concurrent;
using System.Reflection;
using Bencodex.Types;
using Libplanet.Action.State;

namespace Libplanet.Action;

public abstract record class ActionBase : IAction
{
    private static readonly ConcurrentDictionary<Type, long> _gasUsageByType = [];

    protected ActionBase()
    {
    }

    // IValue IAction.PlainValue => new List(
    //     TypeId,
    //     ModelSerializer.Serialize(this));

    private IValue TypeId =>
        GetType().GetCustomAttribute<ActionTypeAttribute>() is { } attribute
            ? attribute.TypeIdentifier
            : throw new InvalidOperationException(
                $"Type is missing {nameof(ActionTypeAttribute)}: {GetType()}");

    // void IAction.LoadPlainValue(IValue plainValue)
    //     => throw new UnreachableException("This method should not be called.");

    IWorld IAction.Execute(IActionContext context)
    {
        using var worldContext = new WorldContext(context);
        UseGas(GetType());
        OnExecute(worldContext, context);
        return worldContext.Flush();
    }

    protected abstract void OnExecute(IWorldContext world, IActionContext context);

    private static void UseGas(Type type)
    {
        var gasUsage = GetGasUsage(type);
        if (gasUsage == 0)
        {
            return;
        }

        GasTracer.UseGas(gasUsage);
    }

    private static long GetGasUsage(Type type)
    {
        return _gasUsageByType.GetOrAdd(type, CalculateGasUsage);

        static long CalculateGasUsage(Type type)
        {
            if (type.GetCustomAttribute<GasUsageAttribute>() is { } attribute)
            {
                return attribute.Amount;
            }

            return 0;
        }
    }
}
