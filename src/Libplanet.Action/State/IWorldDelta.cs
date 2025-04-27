using Libplanet.Crypto;

namespace Libplanet.Action.State;

public interface IWorldDelta
{
    ImmutableDictionary<Address, IAccount> Accounts { get; }

    IWorldDelta SetAccount(Address address, IAccount account);
}
