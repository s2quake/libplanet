using Libplanet.Types.Consensus;
using static Libplanet.Action.SystemAddresses;

namespace Libplanet.Action.Tests.Common;

public sealed record class SetValidator : ActionBase
{
    public required Validator Validator { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var validatorSet = (ImmutableSortedSet<Validator>)world[ValidatorSet, ValidatorSet];
        world[ValidatorSet, ValidatorSet] = validatorSet.Add(Validator);
    }
}
