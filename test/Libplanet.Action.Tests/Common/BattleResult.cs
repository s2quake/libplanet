using Libplanet.Serialization;

namespace Libplanet.Action.Tests.Common;

[Model(Version = 1)]
public sealed record class BattleResult : IEquatable<BattleResult>
{
    [Property(0)]
    public ImmutableSortedSet<string> UsedWeapons { get; init; } = [];

    [Property(1)]
    public ImmutableSortedSet<string> Targets { get; init; } = [];

    public bool Equals(BattleResult? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);
}
