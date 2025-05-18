using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Types.Crypto;

namespace Libplanet.Node;

public interface IActionProvider
{
    IActionLoader ActionLoader { get; }

    PolicyActions PolicyActions { get; }

    IAction[] GetGenesisActions(Address genesisAddress, Address[] validators);
}
