using System.Collections.Immutable;
using Libplanet.Action;

namespace Libplanet.Node.Tests;

public sealed class DumbActionPolicyActionsRegistry : IPolicyActionsRegistry
{
    public ImmutableArray<IAction> BeginBlockActions { get; } = [];

    public ImmutableArray<IAction> EndBlockActions { get; } = [];

    public ImmutableArray<IAction> BeginTxActions { get; } = [];

    public ImmutableArray<IAction> EndTxActions { get; } = [];
}
