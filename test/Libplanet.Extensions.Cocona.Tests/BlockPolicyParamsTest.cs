using Libplanet.Action;
using Libplanet.Blockchain.Policies;
using Xunit;

namespace Libplanet.Extensions.Cocona.Tests;

public class BlockPolicyParamsTest
{
    [Fact]
    public void DefaultState()
    {
        var blockPolicyParams = new BlockPolicyParams();
        var policyActions = blockPolicyParams.GetPolicyActions();
        Assert.Null(blockPolicyParams.GetBlockPolicy());
        Assert.Empty(policyActions.BeginBlockActions);
        Assert.Empty(policyActions.EndBlockActions);
        Assert.Empty(policyActions.BeginBlockActions);
        Assert.Empty(policyActions.EndTxActions);
    }

    [Fact]
    public void GetBlockPolicy()
    {
        var blockPolicyParams = new BlockPolicyParams
        {
            PolicyFactory = $"{GetType().FullName}.{nameof(BlockPolicyFactory)}",
        };
        BlockPolicy blockPolicy = Assert.IsType<BlockPolicy>(
            blockPolicyParams.GetBlockPolicy(new[] { GetType().Assembly })
        );
        Assert.Single(blockPolicy.PolicyActions.BeginBlockActions);
        Assert.IsType<NullAction>(blockPolicy.PolicyActions.BeginBlockActions[0]);
        Assert.Single(blockPolicy.PolicyActions.EndBlockActions);
        Assert.IsType<NullAction>(blockPolicy.PolicyActions.EndBlockActions[0]);
        Assert.Single(blockPolicy.PolicyActions.BeginTxActions);
        Assert.IsType<NullAction>(blockPolicy.PolicyActions.BeginTxActions[0]);
        Assert.Single(blockPolicy.PolicyActions.EndTxActions);
        Assert.IsType<NullAction>(blockPolicy.PolicyActions.EndTxActions[0]);
    }

    [Fact]
    public void GetBlockPolicy_NonQualifiedName()
    {
        var blockPolicyParams = new BlockPolicyParams
        {
            PolicyFactory = nameof(BlockPolicyFactory),
        };
        var e = Assert.Throws<TypeLoadException>(() =>
            blockPolicyParams.GetBlockPolicy(new[] { GetType().Assembly }));
        Assert.Contains("qualified name", e.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void GetBlockPolicy_ReferringNonExistentType()
    {
        var blockPolicyParams = new BlockPolicyParams
        {
            PolicyFactory = $"{GetType().FullName}__NonExistent__.{nameof(BlockPolicyFactory)}",
        };
        var e = Assert.Throws<TypeLoadException>(() =>
            blockPolicyParams.GetBlockPolicy(new[] { GetType().Assembly }));
        Assert.Contains(
            "failed to load policy type",
            e.Message,
            StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void GetBlockPolicy_ReferringNonExistentMethod()
    {
        var blockPolicyParams = new BlockPolicyParams
        {
            PolicyFactory = $"{GetType().FullName}.{nameof(BlockPolicyFactory)}__NonExistent__",
        };
        var e = Assert.Throws<TypeLoadException>(() =>
            blockPolicyParams.GetBlockPolicy(new[] { GetType().Assembly }));
        Assert.Contains(
            "failed to find a static method",
            e.Message,
            StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void GetBlockPolicy_ReferringInstanceMethod()
    {
        var blockPolicyParams = new BlockPolicyParams
        {
            PolicyFactory = $"{GetType().FullName}.{nameof(BlockPolicyFactoryInstanceMethod)}",
        };
        var e = Assert.Throws<TypeLoadException>(() =>
            blockPolicyParams.GetBlockPolicy(new[] { GetType().Assembly }));
        Assert.Contains(
            "failed to find a static method",
            e.Message,
            StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void GetBlockPolicy_NotAcceptingMethodWithParams()
    {
        var blockPolicyParams = new BlockPolicyParams
        {
            PolicyFactory = $"{GetType().FullName}.{nameof(BlockPolicyFactoryWithParams)}",
        };
        var e = Assert.Throws<TypeLoadException>(() =>
            blockPolicyParams.GetBlockPolicy(new[] { GetType().Assembly }));
        Assert.Contains("parameters", e.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void GetBlockPolicy_NotAcceptingMethodWithWrongReturnType()
    {
        var blockPolicyParams = new BlockPolicyParams
        {
            PolicyFactory = $"{GetType().FullName}.{nameof(BlockPolicyFactoryWithWrongReturnType)}",
        };
        var e = Assert.Throws<TypeLoadException>(() =>
            blockPolicyParams.GetBlockPolicy(new[] { GetType().Assembly }));
        Assert.Contains("return type", e.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void GetBlockPolicy_FactoryReturningNull()
    {
        var blockPolicyParams = new BlockPolicyParams
        {
            PolicyFactory = $"{GetType().FullName}.{nameof(BlockPolicyFactoryReturningNull)}",
        };
        var e = Assert.Throws<InvalidOperationException>(() =>
            blockPolicyParams.GetBlockPolicy(new[] { GetType().Assembly }));
        Assert.Contains("returned null", e.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void GetPolicyActions()
    {
        var blockPolicyParams = new BlockPolicyParams
        {
            PolicyFactory = $"{GetType().FullName}.{nameof(BlockPolicyFactory)}",
        };
        var policyActions =
            blockPolicyParams.GetPolicyActions(new[] { GetType().Assembly });
        Assert.IsType<NullAction>(Assert.Single(policyActions.BeginBlockActions));
        Assert.IsType<NullAction>(Assert.Single(policyActions.EndBlockActions));
        Assert.IsType<NullAction>(Assert.Single(policyActions.BeginTxActions));
        Assert.IsType<NullAction>(Assert.Single(policyActions.EndTxActions));
    }

    internal static BlockPolicy BlockPolicyFactory() =>
        new BlockPolicy(
            new PolicyActions
            {
                BeginBlockActions = [new NullAction()],
                EndBlockActions = [new NullAction()],
                BeginTxActions = [new NullAction()],
                EndTxActions = [new NullAction()],
            });

    internal static BlockPolicy BlockPolicyFactoryWithParams(bool param) =>
        new BlockPolicy();

    internal static int BlockPolicyFactoryWithWrongReturnType() => 0;

    internal static BlockPolicy BlockPolicyFactoryReturningNull() => null!;

    internal BlockPolicy BlockPolicyFactoryInstanceMethod() => new BlockPolicy();
}
