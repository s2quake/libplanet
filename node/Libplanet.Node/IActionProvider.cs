using Libplanet.State;
using Libplanet.Types;

namespace Libplanet.Node;

public interface IActionProvider
{
    SystemAction SystemAction { get; }

    IAction[] GetGenesisActions(Address genesisAddress, Address[] validators);
}
