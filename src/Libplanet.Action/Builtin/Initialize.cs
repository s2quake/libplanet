using Libplanet.Serialization;
using Libplanet.Types.Consensus;
using static Libplanet.Action.SystemAddresses;

namespace Libplanet.Action.Builtin;

[Model(Version = 1)]
public sealed record class Initialize : ActionBase, IEquatable<Initialize>
{
    [Property(0)]
    public ImmutableArray<AccountState> States { get; init; } = [];

    [Property(1)]
    public ImmutableSortedSet<Validator> Validators { get; init; } = [];

    public override int GetHashCode() => ModelResolver.GetHashCode(this);

    public bool Equals(Initialize? other) => ModelResolver.Equals(this, other);

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

        world[SystemAccount, SystemAddresses.Validators] = Validators;
    }
}
