using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.Sys;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Node;

public sealed class ActionProvider : IActionProvider
{
    public static ActionProvider Default { get; } = new ActionProvider();

    public IActionLoader ActionLoader { get; } = new AggregateTypedActionLoader();

    public PolicyActions PolicyActions { get; } = new PolicyActions();

    public IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys)
    {
        var validators = validatorKeys
            .Select(item => Validator.Create(item, new BigInteger(1000)))
            .ToImmutableSortedSet();
        return
        [
            new Initialize
            {
                Validators = validators,
            },
        ];
    }
}
