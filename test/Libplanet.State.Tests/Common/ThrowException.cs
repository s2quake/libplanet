using Libplanet.Serialization;

namespace Libplanet.State.Tests.Common;

[Model(Version = 1, TypeName = "Tests_ThrowException")]
public sealed record class ThrowException : ActionBase
{
    [Property(0)]
    public bool ThrowOnExecution { get; init; }

    [Property(1)]
    public bool Deterministic { get; init; } = true;

    protected override void OnExecute(IWorldContext world, IActionContext context)
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
    }

    public sealed class SomeException(string message) : Exception(message)
    {
    }
}
