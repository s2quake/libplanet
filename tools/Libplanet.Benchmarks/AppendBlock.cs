using BenchmarkDotNet.Attributes;
using Libplanet.State.Tests.Actions;
using Libplanet.Data;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests;
using Libplanet.Tests.Store;
using Libplanet.Types;

namespace Libplanet.Benchmarks
{
    public class AppendBlock
    {
        private readonly Libplanet.Blockchain _blockChain;
        private readonly ISigner _signer;
        private BlockCommit _lastCommit;
        private Block _block;
        private BlockCommit _commit;

        public AppendBlock()
        {
            var fx = new MemoryRepositoryFixture();
            var repository = new Repository();
            _blockChain = new Blockchain(fx.GenesisBlock, repository, fx.Options);
            _signer = new PrivateKey().AsSigner();
        }

        [IterationSetup(Target = nameof(AppendBlockOneTransactionNoAction))]
        public void PrepareAppendMakeOneTransactionNoAction()
        {
            _blockChain.StagedTransactions.Add(_signer);
            PrepareAppend();
        }

        [IterationSetup(Target = nameof(AppendBlockTenTransactionsNoAction))]
        public void PrepareAppendMakeTenTransactionsNoAction()
        {
            for (var i = 0; i < 10; i++)
            {
                _blockChain.StagedTransactions.Add(new PrivateKey().AsSigner());
            }
            PrepareAppend();
        }

        [IterationSetup(Target = nameof(AppendBlockOneTransactionWithActions))]
        public void PrepareAppendMakeOneTransactionWithActions()
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
            _blockChain.StagedTransactions.Add(signer, @params: new()
            {
                Actions = actions,
            });
            PrepareAppend();
        }

        [IterationSetup(Target = nameof(AppendBlockTenTransactionsWithActions))]
        public void PrepareAppendMakeTenTransactionsWithActions()
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
                _blockChain.StagedTransactions.Add(signer, @params: new()
                {
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
            _block = _blockChain.Propose(_signer);
            _commit = TestUtils.CreateBlockCommit(_block);
        }
    }
}
