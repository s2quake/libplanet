using Libplanet.Action.State;
using Libplanet.Types.Crypto;
using Libplanet.Serialization;

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
        var key = (ReservedAddresses.LegacyAccount, Address);
        var value = world.GetValue(key, BigInteger.Zero);
        world[key] = value + Increment;
    }
}
