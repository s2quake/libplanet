using Libplanet.Action.State;
using Libplanet.Types.Consensus;

namespace Libplanet.Action.Tests.Common;

public sealed record class SetValidator : ActionBase
{
    public required Validator Validator { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var validatorSet = (ImmutableSortedSet<Validator>)world[ReservedAddresses.ValidatorSetAddress, ReservedAddresses.ValidatorSetAddress];
        world[ReservedAddresses.ValidatorSetAddress, ReservedAddresses.ValidatorSetAddress] = validatorSet.Add(Validator);
    }
}
