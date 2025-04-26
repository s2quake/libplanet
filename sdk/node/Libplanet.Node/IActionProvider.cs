using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Crypto;

namespace Libplanet.Node;

public interface IActionProvider
{
    IActionLoader ActionLoader { get; }

    PolicyActionsRegistry PolicyActionsRegistry { get; }

    IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys);
}
