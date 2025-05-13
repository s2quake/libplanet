using Libplanet.Action.State;
using Libplanet.Store.Trie;
using Libplanet.Types.Crypto;

namespace Libplanet.Action;

public sealed record class ValuePath
{
    public required KeyBytes Name { get; init; }

    public required KeyBytes Key { get; init; }

    public static implicit operator ValuePath((string Name, string Key) value) => new()
    {
        Name = KeyConverters.ToStateKey(value.Name),
        Key = KeyConverters.ToStateKey(value.Key),
    };

    public static implicit operator ValuePath((Address Name, Address Key) value) => new()
    {
        Name = KeyConverters.ToStateKey(value.Name),
        Key = KeyConverters.ToStateKey(value.Key),
    };
}
