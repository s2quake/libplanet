using Libplanet.Serialization;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Actions;

[Model("Libplanet_State_Tests_Actions_SetValidator", Version = 1)]
public sealed record class SetValidator : ActionBase
{
    [Property(0)]
    public required Validator Validator { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var validators = (ImmutableSortedSet<Validator>)world[SystemAccount, ValidatorsKey];
        world[SystemAccount, ValidatorsKey] = validators.Add(Validator);
    }
}
