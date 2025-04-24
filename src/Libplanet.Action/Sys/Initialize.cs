using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Consensus;

namespace Libplanet.Action.Sys;

[ActionType(2)]
[Model(Version = 1)]
public sealed record class Initialize : ActionBase
{
    [Property(0)]
    public ImmutableDictionary<Address, IValue> States { get; init; }
        = ImmutableDictionary<Address, IValue>.Empty;

    [Property(1)]
    public ImmutableSortedSet<Validator> Validators { get; init; } = [];

    // public IWorld Execute(IActionContext context)
    // {
    //     IWorld world = context.PreviousState;

    //     if (context.BlockHeight != 0)
    //     {
    //         throw new InvalidOperationException(
    //             $"{nameof(Initialize)} action can be executed only genesis block."
    //         );
    //     }

    //     if (ImmutableSortedSet<Validator> is { } vs)
    //     {
    //         var validatorSet = world.GetValidatorSet();
    //         foreach (Validator v in vs.Validators)
    //         {
    //             validatorSet = validatorSet.Update(v);
    //         }

    //         world = world.SetValidatorSet(validatorSet);
    //     }

    //     IAccount legacyAccount = world.GetAccount(ReservedAddresses.LegacyAccount);

    //     if (States is { } s)
    //     {
    //         foreach (KeyValuePair<Address, IValue> kv in s)
    //         {
    //             legacyAccount = legacyAccount.SetState(kv.Key, kv.Value);
    //         }
    //     }

    //     world = world.SetAccount(ReservedAddresses.LegacyAccount, legacyAccount);
    //     return world;
    // }


    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        throw new NotImplementedException();
    }
}
