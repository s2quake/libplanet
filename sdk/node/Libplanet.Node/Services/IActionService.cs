using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Types.Crypto;

namespace Libplanet.Node.Services;

public interface IActionService
{
    IActionLoader ActionLoader { get; }

    PolicyActions PolicyActions { get; }

    IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys);
}
