using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Crypto;

namespace Libplanet.Node.Services;

public interface IActionService
{
    IActionLoader ActionLoader { get; }

    IPolicyActionsRegistry PolicyActionsRegistry { get; }

    IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys);
}
