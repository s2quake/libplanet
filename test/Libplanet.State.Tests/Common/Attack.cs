using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.State.Tests.Common;

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
        var battleResult = world.GetValueOrDefault<BattleResult>(SystemAddresses.SystemAccount, TargetAddress, new());
        world[SystemAddresses.SystemAccount, TargetAddress] = battleResult with
        {
            UsedWeapons = battleResult.UsedWeapons.Add(Weapon),
            Targets = battleResult.Targets.Add(Target),
        };
    }
}
