using Libplanet.Action.State;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;
using static Libplanet.Action.State.ReservedAddresses;

namespace Libplanet.Action.Tests.Common;

[Model(Version = 1)]
public sealed record class UpdateValueAction : ActionBase
{
    [Property(0)]
    public Address Address { get; init; }

    [Property(1)]
    public BigInteger Increment { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var value = world.GetValue(LegacyAccount, Address, BigInteger.Zero);
        world[LegacyAccount, Address] = value + Increment;
    }
}
