using System.Diagnostics;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Consensus;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State;

public sealed record class AccountState : IAccountState
{
    private readonly ITrie _trie;
    private readonly ActivitySource _activitySource;

    public AccountState(ITrie trie)
    {
        _trie = trie;
        _activitySource = new ActivitySource("Libplanet.Action.State");
    }

    public ITrie Trie => _trie;

    public IValue GetState(Address address)
    {
        using Activity? a = _activitySource
            .StartActivity(ActivityKind.Internal)?
            .AddTag("Address", address.ToString());
        return Trie[ToStateKey(address)];
    }

    public IValue[] GetStates(IEnumerable<Address> addresses) => [.. addresses.Select(GetState)];

    public ImmutableSortedSet<Validator> GetValidatorSet()
    {
        var value = Trie[ValidatorSetKey];
        return [.. BencodexUtility.ToObjects(value, ModelSerializer.Deserialize<Validator>)];
    }
}
