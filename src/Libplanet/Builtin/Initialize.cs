using Libplanet.Serialization;
using Libplanet.State;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.Builtin;

[Model(Version = 1, TypeName = "Initialize")]
public sealed partial record class Initialize : ActionBase
{
    [Property(0)]
    public ImmutableArray<AccountState> States { get; init; } = [];

    [Property(1)]
    public ImmutableSortedSet<Validator> Validators { get; init; } = [];

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        if (context.BlockHeight != 0)
        {
            throw new InvalidOperationException(
                $"{nameof(Initialize)} action can be executed only genesis block.");
        }

        foreach (var state in States)
        {
            var account = world[state.Name];
            foreach (var (key, value) in state.Values)
            {
                account[key] = value;
            }
        }

        world[SystemAccount, ValidatorsKey] = Validators;
    }
}
