using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Crypto;

namespace Libplanet.Node;

public interface IActionProvider
{
    IActionLoader ActionLoader { get; }

    IPolicyActionsRegistry PolicyActionsRegistry { get; }

    IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys);
}
