using Libplanet.Action.State;

namespace Libplanet.Action.Tests.Common;

public sealed record class ThrowException : IAction
{
    public bool ThrowOnExecution { get; init; }

    public bool Deterministic { get; init; } = true;

    public IWorld Execute(IActionContext context)
    {
        if (ThrowOnExecution)
        {
            if (Deterministic)
            {
                throw new SomeException("An expected exception");
            }
            else
            {
                throw new OutOfMemoryException();
            }
        }

        return context.World;
    }

    public sealed class SomeException(string message) : Exception(message)
    {
    }
}
