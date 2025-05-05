using Bencodex.Types;
using Libplanet.Serialization;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using static Libplanet.Action.State.ReservedAddresses;

namespace Libplanet.Action.Sys;

[Model(Version = 1)]
public sealed record class Initialize : ActionBase, IEquatable<Initialize>
{
    public ImmutableDictionary<Address, IValue> States { get; init; }
        = ImmutableDictionary<Address, IValue>.Empty;

    [Property(0)]
    public ImmutableSortedSet<Validator> Validators { get; init; } = [];

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public bool Equals(Initialize? other) => ModelUtility.Equals(this, other);

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
