using Bencodex.Types;
using Libplanet.Crypto;

namespace Libplanet.Action.State;

public interface IAccount : IAccountState
{
    IAccount SetState(Address address, IValue state);

    IAccount RemoveState(Address address);
}
