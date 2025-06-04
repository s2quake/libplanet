using Libplanet.Serialization;

namespace Libplanet.State.Builtin;

[Model(Version = 1, TypeName = "NullAction")]
public sealed record class NullAction : ActionBase
{
    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
    }
}
