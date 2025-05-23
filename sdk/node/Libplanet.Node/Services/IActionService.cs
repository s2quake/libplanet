using Libplanet.Action;
using Libplanet.Types.Crypto;

namespace Libplanet.Node.Services;

public interface IActionService
{
    PolicyActions PolicyActions { get; }

    IAction[] GetGenesisActions(Address genesisAddress, Address[] validators);
}
