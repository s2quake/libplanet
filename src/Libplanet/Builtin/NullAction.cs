using Libplanet.Serialization;
using Libplanet.State;

namespace Libplanet.Builtin;

[Model("NullAction", Version = 1)]
public sealed record class NullAction : ActionBase
{
    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
    }
}
