using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Crypto;

namespace Libplanet.Action.Tests.Common;

public sealed record class UpdateValueAction : ActionBase
{
    public Address Address { get; init; }

    public Integer Increment { get; init; }

    public IWorld Execute(IActionContext context)
    {
        var world = context.World;
        var account = world.GetAccount(ReservedAddresses.LegacyAccount);
        Integer value = account.GetState(Address) is Integer integer
            ? integer + Increment
            : Increment;

        account = account.SetState(Address, value);
        return world.SetAccount(ReservedAddresses.LegacyAccount, account);
    }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var value = world.GetValue(ReservedAddresses.LegacyAccount, Address, new Integer(0));
        world[ReservedAddresses.LegacyAccount, Address] = (Integer)(value + Increment);
    }
}
