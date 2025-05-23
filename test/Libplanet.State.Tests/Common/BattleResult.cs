using Libplanet.Serialization;

namespace Libplanet.State.Tests.Common;

[Model(Version = 1)]
public sealed record class BattleResult : IEquatable<BattleResult>
{
    [Property(0)]
    public ImmutableSortedSet<string> UsedWeapons { get; init; } = [];

    [Property(1)]
    public ImmutableSortedSet<string> Targets { get; init; } = [];

    public bool Equals(BattleResult? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
