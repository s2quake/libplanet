using Libplanet.Serialization;

namespace Libplanet.Action;

[Model(Version = 1)]
public sealed record class State : IEquatable<State>
{
    [Property(0)]
    public required string Name { get; init; } = string.Empty;

    [Property(1)]
    public ImmutableSortedDictionary<string, object> Values { get; init; }
        = ImmutableSortedDictionary<string, object>.Empty;

    public override int GetHashCode() => ModelResolver.GetHashCode(this);

    public bool Equals(State? other) => ModelResolver.Equals(this, other);
}
