using Libplanet.Serialization;

namespace Libplanet.State.Builtin;

[Model(Version = 1)]
public sealed partial record class NullAction : ActionBase
{
    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
    }
}
