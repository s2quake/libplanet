using Libplanet.Types.Consensus;
using static Libplanet.Action.State.ReservedAddresses;

namespace Libplanet.Action.Tests.Common;

public sealed record class SetValidator : ActionBase
{
    public required Validator Validator { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var validatorSet = (ImmutableSortedSet<Validator>)world[ValidatorSetAddress, ValidatorSetAddress];
        world[ValidatorSetAddress, ValidatorSetAddress] = validatorSet.Add(Validator);
    }
}
