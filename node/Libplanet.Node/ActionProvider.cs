using Libplanet.State;
using Libplanet.Types;

namespace Libplanet.Node;

public sealed class ActionProvider : IActionProvider
{
    public static ActionProvider Default { get; } = new ActionProvider();

    public SystemAction SystemAction { get; } = new SystemAction();

    public IAction[] GetGenesisActions(Address genesisAddress, Address[] validators) => [];
}
