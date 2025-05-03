using Libplanet.Action.State;
using Libplanet.Types.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Action.Tests.Common;

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
}
