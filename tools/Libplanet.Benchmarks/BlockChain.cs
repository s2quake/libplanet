using BenchmarkDotNet.Attributes;
using Libplanet.Action;
using Libplanet.Blockchain.Policies;
using Libplanet.Tests.Store;
using Libplanet.Types.Crypto;

namespace Libplanet.Benchmarks
{
    public class BlockChain
    {
        private StoreFixture _fx;
        private Libplanet.Blockchain.BlockChain _blockChain;

        public BlockChain()
        {
        }

        [IterationCleanup]
        public void FinalizeFixture()
        {
            _fx.Dispose();
        }

        [IterationSetup(Target = nameof(ContainsBlock))]
        public void SetupChain()
        {
            _fx = new DefaultStoreFixture();
            _blockChain = Libplanet.Blockchain.BlockChain.Create(
                BlockPolicy.Empty,
                _fx.Store,
                _fx.StateStore,
                _fx.GenesisBlock);
            var key = new PrivateKey();
            for (var i = 0; i < 500; i++)
            {
                _blockChain.ProposeBlock(key);
            }
        }

        [Benchmark]
        public void ContainsBlock()
        {
            _blockChain.ContainsBlock(_blockChain.Tip.BlockHash);
        }
    }
}
