using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Crypto;

namespace Libplanet.Node.Tests;

public sealed class DumbActionProvider : IActionProvider
{
    public IActionLoader ActionLoader { get; } = new DumbActionLoader();

    public PolicyActions PolicyActions { get; } = new PolicyActions();

    public IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys)
        => ActionProvider.Default.GetGenesisActions(genesisAddress, validatorKeys);
}
