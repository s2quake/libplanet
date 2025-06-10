using Libplanet.Serialization;

namespace Libplanet.State.Tests.Actions;

[Model(Version = 1, TypeName = "Tests_Sleep")]
public sealed record class Sleep : ActionBase
{
    [Property(0)]
    public int ZoneId { get; set; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        // Do nothing
    }
}
