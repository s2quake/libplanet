using Libplanet.State;
using Libplanet.Types;

namespace Libplanet.Node;

public interface IActionProvider
{
    SystemActions PolicyActions { get; }

    IAction[] GetGenesisActions(Address genesisAddress, Address[] validators);
}
