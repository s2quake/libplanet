using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Action.Tests.Common;

[ActionType("attack")]
[Model(Version = 1)]
public sealed record class Attack : ActionBase
{
    [Property(0)]
    public string Weapon { get; init; } = string.Empty;

    [Property(1)]
    public string Target { get; init; } = string.Empty;

    [Property(2)]
    public Address TargetAddress { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var battleResult = world.GetValue<BattleResult>(ReservedAddresses.LegacyAccount, TargetAddress, new());
        world[ReservedAddresses.LegacyAccount, TargetAddress] = battleResult with
        {
            UsedWeapons = battleResult.UsedWeapons.Add(Weapon),
            Targets = battleResult.Targets.Add(Target),
        };
    }

    // public override IWorld Execute(IActionContext context)
    // {
    //     IImmutableSet<string> usedWeapons = ImmutableHashSet<string>.Empty;
    //     IImmutableSet<string> targets = ImmutableHashSet<string>.Empty;
    //     IWorld previousState = context.World;
    //     IAccount legacyAccount = previousState.GetAccount(ReservedAddresses.LegacyAccount);

    //     object value = legacyAccount.GetState(TargetAddress);
    //     if (!ReferenceEquals(value, null))
    //     {
    //         var previousResult = BattleResult.FromBencodex((Bencodex.Types.Dictionary)value);
    //         usedWeapons = previousResult.UsedWeapons;
    //         targets = previousResult.Targets;
    //     }

    //     usedWeapons = usedWeapons.Add(Weapon);
    //     targets = targets.Add(Target);
    //     var result = new BattleResult(usedWeapons, targets);
    //     legacyAccount = legacyAccount.SetState(TargetAddress, result.ToBencodex());

    //     return previousState.SetAccount(ReservedAddresses.LegacyAccount, legacyAccount);
    // }
}
