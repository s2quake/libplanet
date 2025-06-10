using Libplanet.Serialization;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Actions;

[Model(Version = 1, TypeName = "Tests_UpdateValue")]
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
