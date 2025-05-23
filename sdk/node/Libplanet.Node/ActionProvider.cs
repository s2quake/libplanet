using Libplanet.Action;
using Libplanet.Action.Builtin;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Node;

public sealed class ActionProvider : IActionProvider
{
    public static ActionProvider Default { get; } = new ActionProvider();

    public PolicyActions PolicyActions { get; } = new PolicyActions();

    public IAction[] GetGenesisActions(Address genesisAddress, Address[] validators) =>
    [
        new Initialize
        {
            Validators = [.. validators.Select(item => Validator.Create(item, new BigInteger(1000)))],
        },
    ];
}
