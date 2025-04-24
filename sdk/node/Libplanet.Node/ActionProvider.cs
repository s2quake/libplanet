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

    public IPolicyActionsRegistry PolicyActionsRegistry { get; } = new PolicyActionsRegistry();

    public IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys)
    {
        var validators = validatorKeys
            .Select(item => new Validator(item, new BigInteger(1000)))
            .ToArray();
        var validatorSet = new ValidatorSet(validators: [.. validators]);
        return
        [
            new Initialize(
                validatorSet: validatorSet,
                states: ImmutableDictionary.Create<Address, IValue>()),
        ];
    }
}
