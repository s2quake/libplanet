using BenchmarkDotNet.Attributes;
using Libplanet.Tests.Store;

namespace Libplanet.Benchmarks
{
    public class BlockChain
    {
        private readonly StoreFixture _fx;
        private readonly Libplanet.Blockchain.BlockChain _blockChain;

        public BlockChain()
        {
        }

        [IterationCleanup]
        public void FinalizeFixture()
        {
            _fx.Dispose();
        }

        // [IterationSetup(Target = nameof(ContainsBlock))]
        // public void SetupChain()
        // {
        //     _fx = new DefaultStoreFixture();
        //     _blockChain = Libplanet.Blockchain.BlockChain.Create(
        //         BlockChainOptions.Empty,
        //         _fx.Store,
        //         _fx.StateStore,
        //         _fx.GenesisBlock);
        //     var key = new PrivateKey();
        //     for (var i = 0; i < 500; i++)
        //     {
        //         _blockChain.ProposeBlock(key);
        //     }
        // }

        [Benchmark]
        public void ContainsBlock()
        {
            _blockChain.ContainsBlock(_blockChain.Tip.BlockHash);
        }
    }
}
