using Libplanet.Serialization;

namespace Libplanet.Action.Tests.Common;

[ActionType("sleep")]
[Model(Version = 1)]
public sealed record class Sleep : ActionBase
{
    [Property(0)]
    public int ZoneId { get; set; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        // Do nothing
    }
}
