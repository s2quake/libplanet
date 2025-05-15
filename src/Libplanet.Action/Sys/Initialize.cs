using Libplanet.Serialization;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using static Libplanet.Action.State.ReservedAddresses;

namespace Libplanet.Action.Sys;

[Model(Version = 1)]
public sealed record class Initialize : ActionBase, IEquatable<Initialize>
{
    public ImmutableDictionary<Address, object> States { get; init; }
        = ImmutableDictionary<Address, object>.Empty;

    [Property(0)]
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

        foreach (var (address, value) in States)
        {
            world[LegacyAccount, address] = value;
        }

        world[ValidatorSetAddress, ValidatorSetAddress] = Validators;
    }
}
