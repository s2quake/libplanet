using Libplanet.Serialization;
using Libplanet.State;

namespace Libplanet.TestUtilities;

[Model("Libplanet_TestUtilities_TestAction", Version = 1)]
public sealed record class TestAction : ActionBase
{
    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
    }
}
