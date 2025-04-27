using Libplanet.Crypto;

namespace Libplanet.Action.State;

public interface IWorld : IWorldState
{
    IWorldDelta Delta { get; }

    IAccount GetAccount(Address address);

    IWorld SetAccount(Address address, IAccount account);
}
