// This code does not compile because it is used by ActionServiceTest test.
#pragma warning disable MEN008 // A file's name should match or include the name of the main type it contains.
using System.Collections.Immutable;
using System.Numerics;
using System.Linq;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.State;
using Libplanet.Action.Sys;
using Libplanet.Crypto;
using Libplanet.Types.Consensus;
using Libplanet.Node.Services;

namespace Libplanet.Node.DumbActions;

public class DumbAction : IAction
{
    public IValue PlainValue => Dictionary.Empty;

    public void LoadPlainValue(IValue plainValue)
    {
        // Do nothing.
    }

    public IWorld Execute(IActionContext context) =>
        context.PreviousState;
}

public sealed class DumbBeginAction : DumbAction
{
}

public sealed class DumbEndAction : DumbAction
{
}

public sealed class DumbBeginTxAction : DumbAction
{
}

public sealed class DumbEndTxAction : DumbAction
{
}

public sealed class DumbActionLoader : IActionLoader
{
    public IAction LoadAction(IValue value)
    {
        if (Registry.IsSystemAction(value))
        {
            return Registry.Deserialize(value);
        }

        return new DumbAction();
    }
}

public sealed class DumbActionPolicyActions : PolicyActions
{
    public ImmutableArray<IAction> BeginBlockActions => [new DumbBeginAction()];

    public ImmutableArray<IAction> EndBlockActions => [new DumbEndAction()];

    public ImmutableArray<IAction> BeginTxActions => [new DumbBeginTxAction()];

    public ImmutableArray<IAction> EndTxActions => [new DumbEndTxAction()];
}

public sealed class DumbActionProvider : IActionProvider
{
    public IActionLoader ActionLoader { get; } = new DumbActionLoader();

    public PolicyActions PolicyActions { get; }
        = new DumbActionPolicyActions();

    public IAction[] GetGenesisActions(Address genesisAddress, PublicKey[] validatorKeys)
        => ActionProvider.Default.GetGenesisActions(genesisAddress, validatorKeys);
}
