using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Action.Tests.Common;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Tests.Blockchain.Renderers;

public class AnonymousActionRendererTest
{
    private static readonly IValue _action = ModelSerializer.Serialize(new DumbAction());

    private static readonly World _world = World.Create();

    private static readonly CommittedActionContext _actionContext =
        CommittedActionContext.Create(
            new ActionContext
            {
                BlockHeight = 0,
                BlockProtocolVersion = BlockHeader.CurrentProtocolVersion,
            },
            _world.Trie.Hash);

    private static readonly Exception _exception = new Exception();

    private static readonly Block _genesis =
        TestUtils.ProposeGenesisBlock(TestUtils.GenesisProposer);

    private static readonly Block _blockA =
        TestUtils.ProposeNextBlock(_genesis, TestUtils.GenesisProposer);

    [Fact]
    public void ActionRenderer()
    {
        (IValue, CommittedActionContext, HashDigest<SHA256>)? record = null;
        // var renderer = new AnonymousActionRenderer
        // {
        //     ActionRenderer = (action, context, nextState) =>
        //         record = (action, context, nextState),
        // };

        // renderer.RenderActionError(_action, _actionContext, _exception);
        // Assert.Null(record);
        // renderer.RenderBlock(_genesis, _blockA);
        // Assert.Null(record);

        // renderer.RenderAction(_action, _actionContext, _world.Trie.Hash);
        Assert.NotNull(record);
        Assert.Same(_action, record?.Item1);
        Assert.Same(_actionContext, record?.Item2);
        Assert.Equal(_world.Trie.Hash, record?.Item3);
    }

    [Fact]
    public void ActionErrorRenderer()
    {
        (IValue, CommittedActionContext, Exception)? record = null;
        // var renderer = new AnonymousActionRenderer
        // {
        //     ActionErrorRenderer = (action, context, exception) =>
        //         record = (action, context, exception),
        // };

        // renderer.RenderAction(_action, _actionContext, _world.Trie.Hash);
        // Assert.Null(record);
        // renderer.RenderBlock(_genesis, _blockA);
        // Assert.Null(record);

        // renderer.RenderActionError(_action, _actionContext, _exception);
        Assert.NotNull(record);
        Assert.Same(_action, record?.Item1);
        Assert.Same(_actionContext, record?.Item2);
        Assert.Same(_exception, record?.Item3);
    }

    [Fact]
    public void BlockRenderer()
    {
        (Block Old, Block New)? record = null;
        // var renderer = new AnonymousActionRenderer
        // {
        //     BlockRenderer = (oldTip, newTip) => record = (oldTip, newTip),
        // };

        // renderer.RenderAction(_action, _actionContext, _world.Trie.Hash);
        // Assert.Null(record);
        // renderer.RenderActionError(_action, _actionContext, _exception);
        // Assert.Null(record);

        // renderer.RenderBlock(_genesis, _blockA);
        Assert.NotNull(record);
        Assert.Same(_genesis, record?.Old);
        Assert.Same(_blockA, record?.New);
    }
}
