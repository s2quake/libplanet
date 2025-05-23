using BenchmarkDotNet.Attributes;
using Libplanet.State.Tests.Common;
using Libplanet.Blockchain;
using Libplanet.Data;
using Libplanet.Tests;
using Libplanet.Tests.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Benchmarks
{
    public class ProposeBlock
    {
        private readonly Libplanet.Blockchain.BlockChain _blockChain;
        private readonly PrivateKey _privateKey;
        private BlockCommit _lastCommit;
        private Block _block;

        public ProposeBlock()
        {
            var fx = new MemoryStoreFixture();
            var repository = new Repository();
            _blockChain = new Libplanet.Blockchain.BlockChain(fx.GenesisBlock, repository, fx.Options);
            _privateKey = new PrivateKey();
        }

        [IterationSetup(Target = nameof(ProposeBlockEmpty))]
        public void PreparePropose()
        {
            _lastCommit = TestUtils.CreateBlockCommit(_blockChain.Tip);
        }

        [IterationCleanup(
            Targets = new []
            {
                nameof(ProposeBlockEmpty),
                nameof(ProposeBlockOneTransactionNoAction),
                nameof(ProposeBlockTenTransactionsNoAction),
                nameof(ProposeBlockOneTransactionWithActions),
                nameof(ProposeBlockTenTransactionsWithActions),
            })]
        public void CleanupPropose()
        {
            // To unstaging transactions, a block is appended to blockchain.
            _blockChain.Append(_block, TestUtils.CreateBlockCommit(_block));
        }

        [IterationSetup(Target = nameof(ProposeBlockOneTransactionNoAction))]
        public void MakeOneTransactionNoAction()
        {
            PreparePropose();
            var tx = new TransactionBuilder
            {
                Signer = _privateKey,
                Blockchain = _blockChain,
            }.Create();
            _blockChain.StagedTransactions.Add(tx);
        }

        [IterationSetup(Target = nameof(ProposeBlockTenTransactionsNoAction))]
        public void MakeTenTransactionsNoAction()
        {
            for (var i = 0; i < 10; i++)
            {
                var tx = new TransactionBuilder
                {
                    Signer = new PrivateKey(),
                    Blockchain = _blockChain,
                }.Create();
                _blockChain.StagedTransactions.Add(tx);
            }
            PreparePropose();
        }

        [IterationSetup(Target = nameof(ProposeBlockOneTransactionWithActions))]
        public void MakeOneTransactionWithActions()
        {
            var privateKey = new PrivateKey();
            var address = privateKey.Address;
            var actions = new[]
            {
                DumbAction.Create((address, "foo")),
                DumbAction.Create((address, "bar")),
                DumbAction.Create((address, "baz")),
                DumbAction.Create((address, "qux")),
            };
            var tx = new TransactionBuilder
            {
                Signer = privateKey,
                Blockchain = _blockChain,
                Actions = actions,
            }.Create();
            _blockChain.StagedTransactions.Add(tx);
            PreparePropose();
        }

        [IterationSetup(Target = nameof(ProposeBlockTenTransactionsWithActions))]
        public void MakeTenTransactionsWithActions()
        {
            for (var i = 0; i < 10; i++)
            {
                var privateKey = new PrivateKey();
                var address = privateKey.Address;
                var actions = new[]
                {
                    DumbAction.Create((address, "foo")),
                    DumbAction.Create((address, "bar")),
                    DumbAction.Create((address, "baz")),
                    DumbAction.Create((address, "qux")),
                };
                var tx = new TransactionBuilder
                {
                    Signer = privateKey,
                    Blockchain = _blockChain,
                    Actions = actions,
                }.Create();
                _blockChain.StagedTransactions.Add(tx);
            }
            PreparePropose();
        }

        [Benchmark]
        public void ProposeBlockEmpty()
        {
            _block = _blockChain.ProposeBlock(_privateKey);
        }

        [Benchmark]
        public void ProposeBlockOneTransactionNoAction()
        {
            _block = _blockChain.ProposeBlock(_privateKey);
        }

        [Benchmark]
        public void ProposeBlockTenTransactionsNoAction()
        {
            _block = _blockChain.ProposeBlock(_privateKey);
        }

        [Benchmark]
        public void ProposeBlockOneTransactionWithActions()
        {
            _block = _blockChain.ProposeBlock(_privateKey);
        }

        [Benchmark]
        public void ProposeBlockTenTransactionsWithActions()
        {
            _block = _blockChain.ProposeBlock(_privateKey);
        }
    }
}
