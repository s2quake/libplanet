using System.Collections.Concurrent;
using System.Reflection;

namespace Libplanet.Action;

public abstract record class ActionBase : IAction
{
    private static readonly ConcurrentDictionary<Type, long> _gasUsageByType = [];

    protected ActionBase()
    {
    }

    void IAction.Execute(IWorldContext world, IActionContext context)
    {
        UseGas(GetType());
        OnExecute(world, context);
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
