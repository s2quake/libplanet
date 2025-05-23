using Libplanet.Action;
using Libplanet.Action.Builtin;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Node;

public sealed class ActionProvider : IActionProvider
{
    public static ActionProvider Default { get; } = new ActionProvider();

    public SystemActions PolicyActions { get; } = new SystemActions();

    public IAction[] GetGenesisActions(Address genesisAddress, Address[] validators) =>
    [
        new Initialize
        {
            Validators = [.. validators.Select(item => Validator.Create(item, new BigInteger(1000)))],
        },
    ];
}
