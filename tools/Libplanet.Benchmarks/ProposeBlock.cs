using BenchmarkDotNet.Attributes;
using Libplanet.State.Tests.Actions;
using Libplanet.Data;
using Libplanet.Tests;
using Libplanet.Tests.Store;
using Libplanet.Types;

namespace Libplanet.Benchmarks
{
    public class ProposeBlock
    {
        private readonly Libplanet.Blockchain _blockChain;
        private readonly ISigner _signer;
        private BlockCommit _lastCommit;
        private Block _block;

        public ProposeBlock()
        {
            var fx = new MemoryRepositoryFixture();
            var repository = new Repository();
            _blockChain = new Blockchain(fx.GenesisBlock, repository, fx.Options);
            _signer = new PrivateKey().AsSigner();
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
            }.Create(_signer, _blockChain);
            _blockChain.StagedTransactions.Add(tx);
        }

        [IterationSetup(Target = nameof(ProposeBlockTenTransactionsNoAction))]
        public void MakeTenTransactionsNoAction()
        {
            for (var i = 0; i < 10; i++)
            {
                var tx = new TransactionBuilder
                {
                }.Create(new PrivateKey().AsSigner(), _blockChain);
                _blockChain.StagedTransactions.Add(tx);
            }
            PreparePropose();
        }

        [IterationSetup(Target = nameof(ProposeBlockOneTransactionWithActions))]
        public void MakeOneTransactionWithActions()
        {
            var signer = new PrivateKey().AsSigner();
            var address = signer.Address;
            var actions = new[]
            {
                DumbAction.Create((address, "foo")),
                DumbAction.Create((address, "bar")),
                DumbAction.Create((address, "baz")),
                DumbAction.Create((address, "qux")),
            };
            var tx = new TransactionBuilder
            {
                Actions = actions,
            }.Create(signer, _blockChain);
            _blockChain.StagedTransactions.Add(tx);
            PreparePropose();
        }

        [IterationSetup(Target = nameof(ProposeBlockTenTransactionsWithActions))]
        public void MakeTenTransactionsWithActions()
        {
            for (var i = 0; i < 10; i++)
            {
                var signer = new PrivateKey().AsSigner();
                var address = signer.Address;
                var actions = new[]
                {
                    DumbAction.Create((address, "foo")),
                    DumbAction.Create((address, "bar")),
                    DumbAction.Create((address, "baz")),
                    DumbAction.Create((address, "qux")),
                };
                var tx = new TransactionBuilder
                {
                    Actions = actions,
                }.Create(signer, _blockChain);
                _blockChain.StagedTransactions.Add(tx);
            }
            PreparePropose();
        }

        [Benchmark]
        public void ProposeBlockEmpty()
        {
            _block = _blockChain.Propose(_signer);
        }

        [Benchmark]
        public void ProposeBlockOneTransactionNoAction()
        {
            _block = _blockChain.Propose(_signer);
        }

        [Benchmark]
        public void ProposeBlockTenTransactionsNoAction()
        {
            _block = _blockChain.Propose(_signer);
        }

        [Benchmark]
        public void ProposeBlockOneTransactionWithActions()
        {
            _block = _blockChain.Propose(_signer);
        }

        [Benchmark]
        public void ProposeBlockTenTransactionsWithActions()
        {
            _block = _blockChain.Propose(_signer);
        }
    }
}
