using BenchmarkDotNet.Attributes;
using Libplanet.State.Tests.Common;
using Libplanet.Data;
using Libplanet.Tests;
using Libplanet.Tests.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Benchmarks
{
    public class AppendBlock
    {
        private readonly Libplanet.Blockchain.BlockChain _blockChain;
        private readonly PrivateKey _privateKey;
        private BlockCommit _lastCommit;
        private Block _block;
        private BlockCommit _commit;

        public AppendBlock()
        {
            var fx = new MemoryStoreFixture();
            var repository = new Repository();
            _blockChain = new Blockchain.BlockChain(fx.GenesisBlock, repository, fx.Options);
            _privateKey = new PrivateKey();
        }

        [IterationSetup(Target = nameof(AppendBlockOneTransactionNoAction))]
        public void PrepareAppendMakeOneTransactionNoAction()
        {
            _blockChain.StagedTransactions.Add(submission: new()
            {
                Signer = _privateKey,
            });
            PrepareAppend();
        }

        [IterationSetup(Target = nameof(AppendBlockTenTransactionsNoAction))]
        public void PrepareAppendMakeTenTransactionsNoAction()
        {
            for (var i = 0; i < 10; i++)
            {
                _blockChain.StagedTransactions.Add(submission: new()
                {
                    Signer = new PrivateKey()
                });
            }
            PrepareAppend();
        }

        [IterationSetup(Target = nameof(AppendBlockOneTransactionWithActions))]
        public void PrepareAppendMakeOneTransactionWithActions()
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
            _blockChain.StagedTransactions.Add(submission: new()
            {
                Signer = privateKey,
                Actions = actions,
            });
            PrepareAppend();
        }

        [IterationSetup(Target = nameof(AppendBlockTenTransactionsWithActions))]
        public void PrepareAppendMakeTenTransactionsWithActions()
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
                _blockChain.StagedTransactions.Add(submission: new()
                {
                    Signer = privateKey,
                    Actions = actions,
                });
            }
            PrepareAppend();
        }

        [Benchmark]
        public void AppendBlockOneTransactionNoAction()
        {
            _blockChain.Append(_block, blockCommit: _commit);
        }

        [Benchmark]
        public void AppendBlockTenTransactionsNoAction()
        {
            _blockChain.Append(_block, blockCommit: _commit);
        }

        [Benchmark]
        public void AppendBlockOneTransactionWithActions()
        {
            _blockChain.Append(_block, blockCommit: _commit);
        }

        [Benchmark]
        public void AppendBlockTenTransactionsWithActions()
        {
            _blockChain.Append(_block, blockCommit: _commit);
        }

        private void PrepareAppend()
        {
            _lastCommit = TestUtils.CreateBlockCommit(_blockChain.Tip);
            _block = _blockChain.ProposeBlock(_privateKey);
            _commit = TestUtils.CreateBlockCommit(_block);
        }
    }
}
