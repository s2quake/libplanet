using Libplanet.Crypto;

namespace Libplanet.Action.State;

public sealed record class WorldDelta : IWorldDelta
{
    public WorldDelta()
        : this(ImmutableDictionary<Address, IAccount>.Empty)
    {
    }

    private WorldDelta(ImmutableDictionary<Address, IAccount> accounts)
    {
        Accounts = accounts;
    }

    public ImmutableDictionary<Address, IAccount> Accounts { get; }

    public IWorldDelta SetAccount(Address address, IAccount account)
        => new WorldDelta(Accounts.SetItem(address, account));
}
