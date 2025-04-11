using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Crypto;
using Libplanet.Node.Actions;
using Libplanet.Node.Options;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class ActionService(IOptions<ActionOptions> options)
    : IActionService
{
    public IActionLoader ActionLoader => ActionProvider.ActionLoader;

    public IPolicyActionsRegistry PolicyActionsRegistry => ActionProvider.PolicyActionsRegistry;

    public IActionProvider ActionProvider { get; } = GetActionProvider(options.Value);

    public IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys)
    {
        return ActionProvider.GetGenesisActions(genesisAddress, validatorKeys);
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

    // private static IActionLoader GetActionLoader(ActionOptions options)
    // {
    //     if (options.ActionLoaderType != string.Empty)
    //     {
    //         var modulePath = options.ModulePath != string.Empty
    //             ? Path.GetFullPath(options.ModulePath) : string.Empty;
    //         var actionLoaderType = options.ActionLoaderType;
    //         return PluginLoader.LoadActionLoader(modulePath, actionLoaderType);
    //     }

    //     return new AggregateTypedActionLoader();
    // }

    // private static IPolicyActionsRegistry GetPolicyActionsRegistry(ActionOptions options)
    // {
    //     if (options.PolicyActionRegistryType != string.Empty)
    //     {
    //         var modulePath = options.ModulePath != string.Empty
    //             ? Path.GetFullPath(options.ModulePath) : string.Empty;
    //         var policyActionRegistryType = options.PolicyActionRegistryType;
    //         return PluginLoader.LoadPolicyActionRegistry(modulePath, policyActionRegistryType);
    //     }

    //     return new PolicyActionsRegistry();
    // }
}
