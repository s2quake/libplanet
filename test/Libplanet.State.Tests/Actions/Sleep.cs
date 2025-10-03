using Libplanet.Serialization;

namespace Libplanet.State.Tests.Actions;

[Model("Libplanet_State_Tests_Actions_Sleep", Version = 1)]
public sealed record class Sleep : ActionBase
{
    [Property(0)]
    public int ZoneId { get; set; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        // Do nothing
    }
}
