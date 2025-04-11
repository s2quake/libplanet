using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Crypto;
using Libplanet.Node.Services;

namespace Libplanet.Node.Tests;

public sealed class DumbActionProvider : IActionProvider
{
    public IActionLoader ActionLoader { get; } = new DumbActionLoader();

    public IPolicyActionsRegistry PolicyActionsRegistry { get; }
        = new DumbActionPolicyActionsRegistry();

    public IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys)
        => ActionProvider.Default.GetGenesisActions(genesisAddress, validatorKeys);
}
