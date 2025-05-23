using Libplanet.Serialization;

namespace Libplanet.State;

[Model(Version = 1)]
public sealed record class AccountState : IEquatable<AccountState>
{
    [Property(0)]
    public required string Name { get; init; } = string.Empty;

    [Property(1)]
    public ImmutableSortedDictionary<string, object> Values { get; init; }
        = ImmutableSortedDictionary<string, object>.Empty;

    public override int GetHashCode() => ModelResolver.GetHashCode(this);

    public bool Equals(AccountState? other) => ModelResolver.Equals(this, other);
}
