using Libplanet.State;
using Libplanet.Types;

namespace Libplanet.Node.Services;

public interface IActionService
{
    SystemActions PolicyActions { get; }

    IAction[] GetGenesisActions(Address genesisAddress, Address[] validators);
}
