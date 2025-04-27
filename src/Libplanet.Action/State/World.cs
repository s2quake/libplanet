using Libplanet.Crypto;
using Libplanet.Store.Trie;

namespace Libplanet.Action.State;

public class World : IWorld
{
    private readonly IWorldState _baseState;

    public World(IWorldState baseState)
        : this(baseState, new WorldDelta())
    {
    }

    private World(IWorldState baseState, IWorldDelta delta)
    {
        _baseState = baseState;
        Delta = delta;
    }

    public IWorldDelta Delta { get; }

    public ITrie Trie => _baseState.Trie;

    public bool Legacy => _baseState.Legacy;

    public int Version => _baseState.Version;

    public IAccount GetAccount(Address address)
    {
        return Delta.Accounts.TryGetValue(address, out IAccount? account)
            ? account
            : new Account(_baseState.GetAccountState(address));
    }

    public IAccountState GetAccountState(Address address) => GetAccount(address);

    public IWorld SetAccount(Address address, IAccount account)
    {
        if (Legacy && !address.Equals(ReservedAddresses.LegacyAccount))
        {
            throw new ArgumentException(
                $"Cannot set a non-legacy account ({address}) to a legacy {nameof(IWorld)}.",
                nameof(address));
        }

        return new World(_baseState, Delta.SetAccount(address, account));
    }
}
