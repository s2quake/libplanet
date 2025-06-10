using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Actions;

public sealed record class SetValidator : ActionBase
{
    public required Validator Validator { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var validatorSet = (ImmutableSortedSet<Validator>)world[SystemAccount, ValidatorsKey];
        world[SystemAccount, ValidatorsKey] = validatorSet.Add(Validator);
    }
}
