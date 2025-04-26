using System.Numerics;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.Sys;
using Libplanet.Crypto;
using Libplanet.Types.Consensus;

namespace Libplanet.Node;

public sealed class ActionProvider : IActionProvider
{
    public static ActionProvider Default { get; } = new ActionProvider();

    public IActionLoader ActionLoader { get; } = new AggregateTypedActionLoader();

    public PolicyActionsRegistry PolicyActionsRegistry { get; } = new PolicyActionsRegistry();

    public IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys)
    {
        var validators = validatorKeys
            .Select(item => new Validator(item, new BigInteger(1000)))
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
