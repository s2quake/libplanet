using Libplanet.State;
using Libplanet.State.Builtin;
using Libplanet.Types;
using Libplanet.Types;

namespace Libplanet.Node;

public sealed class ActionProvider : IActionProvider
{
    public static ActionProvider Default { get; } = new ActionProvider();

    public SystemActions PolicyActions { get; } = new SystemActions();

    public IAction[] GetGenesisActions(Address genesisAddress, Address[] validators) =>
    [
        new Initialize
        {
            Validators = [.. validators.Select(item => new Validator { Address = item, Power = new BigInteger(1000) })],
        },
    ];
}
