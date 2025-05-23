using Libplanet.Action;
using Libplanet.Types.Crypto;

namespace Libplanet.Node;

public interface IActionProvider
{
    SystemActions PolicyActions { get; }

    IAction[] GetGenesisActions(Address genesisAddress, Address[] validators);
}
