using Libplanet.Types;

namespace Libplanet.Tests.Blockchain.Renderers
{
    public class AnonymousRendererTest
    {
        private static readonly Block _genesis =
            TestUtils.ProposeGenesisBlock(TestUtils.GenesisProposerKey);

        private static readonly Block _blockA =
            TestUtils.ProposeNextBlock(_genesis, TestUtils.GenesisProposerKey);

        [Fact]
        public void BlockRenderer()
        {
            (Block Old, Block New)? record = null;
            // var renderer = new AnonymousRenderer
            // {
            //     BlockRenderer = (oldTip, newTip) => record = (oldTip, newTip),
            // };

            // renderer.RenderBlock(_genesis, _blockA);
            Assert.NotNull(record);
            Assert.Same(_genesis, record?.Old);
            Assert.Same(_blockA, record?.New);
        }
    }
}
