using Libplanet.Serialization;

namespace Libplanet.Action;

[Model(Version = 1)]
public sealed record class NullAction : ActionBase
{
    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
    }
}
