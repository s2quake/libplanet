using Libplanet.Serialization;
using Libplanet.State;

namespace Libplanet.TestUtilities;

[Model(Version = 1, TypeName = "Libplanet_TestUtilities_TestAction")]
public sealed record class TestAction : ActionBase
{
    protected override void OnExecute(IWorldContext world, IActionContext context)
    {

    }
}
