using Libplanet.Types.Consensus;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Common;

public sealed record class SetValidator : ActionBase
{
    public required Validator Validator { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var validatorSet = (ImmutableSortedSet<Validator>)world[SystemAccount, Validators];
        world[SystemAccount, Validators] = validatorSet.Add(Validator);
    }
}
