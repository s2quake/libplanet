using System.Diagnostics;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State;

public class WorldBaseState(ITrie trie, IStateStore stateStore) : IWorldState
{
    private readonly IStateStore _stateStore = stateStore;
    private readonly ActivitySource _activitySource = new("Libplanet.Action.WorldBaseState");

    public ITrie Trie { get; } = trie;

    public int Version { get; } = trie.GetMetadata() is { } value ? value.Version : 0;

    public IAccountState GetAccountState(Address address)
    {
        using Activity? a = _activitySource
            .StartActivity(ActivityKind.Internal)?
            .AddTag("Address", address.ToString());

        if (Trie.TryGetValue(ToStateKey(address), out var value) && value is Binary binary)
        {
            return new AccountState(_stateStore.GetStateRoot(new HashDigest<SHA256>(binary.ByteArray)));
        }
        else
        {
            return new AccountState(_stateStore.GetStateRoot(default));
        }
    }
}
