using Libplanet.Serialization;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Actions;

[Model("Libplanet_State_Tests_Actions_UpdateValue", Version = 1)]
public sealed record class UpdateValue : ActionBase
{
    [Property(0)]
    public Address Address { get; init; }

    [Property(1)]
    public BigInteger Increment { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var value = world.GetValueOrDefault(SystemAccount, Address, BigInteger.Zero);
        world[SystemAccount, Address] = value + Increment;
    }
}
