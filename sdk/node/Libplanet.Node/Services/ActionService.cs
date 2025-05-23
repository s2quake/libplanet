using Libplanet.State;
using Libplanet.Node.Actions;
using Libplanet.Node.Options;
using Libplanet.Types.Crypto;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class ActionService(IOptions<ActionOptions> options)
    : IActionService
{
    public SystemActions PolicyActions => ActionProvider.PolicyActions;

    public IActionProvider ActionProvider { get; } = GetActionProvider(options.Value);

    public IAction[] GetGenesisActions(Address genesisAddress, Address[] validators)
    {
        return ActionProvider.GetGenesisActions(genesisAddress, validators);
    }

    private static IActionProvider GetActionProvider(ActionOptions options)
    {
        if (options.ActionProviderType != string.Empty)
        {
            var modulePath = options.ModulePath != string.Empty
                ? Path.GetFullPath(options.ModulePath) : string.Empty;
            var actionProviderType = options.ActionProviderType;
            return PluginLoader.LoadActionProvider(modulePath, actionProviderType);
        }

        return new ActionProvider();
    }
}
