using Libplanet.Serialization;

namespace Libplanet.State;

[Model(Version = 1)]
public sealed partial record class AccountState
{
    [Property(0)]
    public required string Name { get; init; } = string.Empty;

    [Property(1)]
    public ImmutableSortedDictionary<string, object> Values { get; init; }
        = ImmutableSortedDictionary<string, object>.Empty;
}
