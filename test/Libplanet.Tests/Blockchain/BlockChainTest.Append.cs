using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Action.Sys;
using Libplanet.Action.Tests.Common;
using Libplanet.Blockchain;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;
using Serilog;

namespace Libplanet.Tests.Blockchain
{
    public partial class BlockChainTest
    {
        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void Append(bool getTxExecutionViaStore)
        {
            Func<BlockHash, TxId, TxExecution> getTxExecution
                = getTxExecutionViaStore
                ? (Func<BlockHash, TxId, TxExecution>)_blockChain.Store.GetTxExecution
                : _blockChain.GetTxExecution;

            PrivateKey[] keys = Enumerable.Repeat(0, 5).Select(_ => new PrivateKey()).ToArray();
            (Address[] addresses, Transaction[] txs) =
                MakeFixturesForAppendTests(keys: keys);
            var genesis = _blockChain.Genesis;

            Assert.Equal(1, _blockChain.Count);
            // Assert.Empty(_renderer.ActionRecords);
            // Assert.Empty(_renderer.BlockRecords);
            var block1 = _blockChain.ProposeBlock(
                keys[4], TestUtils.CreateBlockCommit(_blockChain.Tip));
            _blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));
            Assert.NotNull(_blockChain.GetBlockCommit(block1.BlockHash));
            Block block2 = _blockChain.ProposeBlock(
                keys[4],
                lastCommit: TestUtils.CreateBlockCommit(block1),
                [.. txs],
                evidences: []);
            foreach (Transaction tx in txs)
            {
                Assert.Null(getTxExecution(genesis.BlockHash, tx.Id));
                Assert.Null(getTxExecution(block1.BlockHash, tx.Id));
                Assert.Null(getTxExecution(block2.BlockHash, tx.Id));
            }

            foreach (var tx in txs)
            {
                Assert.Null(_fx.Store.GetFirstTxIdBlockHashIndex(tx.Id));
            }

            _blockChain.Append(block2, TestUtils.CreateBlockCommit(block2));

            foreach (var tx in txs)
            {
                Assert.True(_fx.Store.GetFirstTxIdBlockHashIndex(tx.Id).Equals(block2.BlockHash));
            }

            Assert.True(_blockChain.ContainsBlock(block2.BlockHash));

            // RenderRecord.ActionSuccess[] renders = _renderer.ActionSuccessRecords
            //     .Where(r => TestUtils.IsDumbAction(r.Action))
            //     .ToArray();
            // DumbAction[] actions = renders.Select(r => TestUtils.ToDumbAction(r.Action)).ToArray();
            // Assert.Equal(4, renders.Length);
            // Assert.True(renders.All(r => r.Render));
            // Assert.Equal("foo", actions[0].Append?.Item);
            // Assert.Equal(2, renders[0].Context.BlockHeight);
            // Assert.Equal(
            //     new IValue[] { null, null, null, null, (Integer)1 },
            //     addresses.Select(_blockChain
            //         .GetWorld(renders[0].Context.PreviousState)
            //         .GetAccount(ReservedAddresses.LegacyAccount)
            //         .GetValue));
            // Assert.Equal(
            //     new IValue[] { (Text)"foo", null, null, null, (Integer)1 },
            //     addresses.Select(_blockChain
            //         .GetWorld(renders[0].NextState)
            //         .GetAccount(ReservedAddresses.LegacyAccount)
            //         .GetValue));
            // Assert.Equal("bar", actions[1].Append?.Item);
            // Assert.Equal(2, renders[1].Context.BlockHeight);
            // Assert.Equal(
            //     addresses.Select(_blockChain
            //         .GetWorld(renders[0].NextState)
            //         .GetAccount(ReservedAddresses.LegacyAccount)
            //         .GetValue),
            //     addresses.Select(_blockChain
            //         .GetWorld(renders[1].Context.PreviousState)
            //         .GetAccount(ReservedAddresses.LegacyAccount)
            //         .GetValue));
            // Assert.Equal(
            //     new IValue[] { (Text)"foo", (Text)"bar", null, null, (Integer)1 },
            //     addresses.Select(
            //         _blockChain.GetWorld(renders[1].NextState)
            //             .GetAccount(ReservedAddresses.LegacyAccount).GetValue));
            // Assert.Equal("baz", actions[2].Append?.Item);
            // Assert.Equal(2, renders[2].Context.BlockHeight);
            // Assert.Equal(
            //     addresses.Select(
            //         _blockChain.GetWorld(renders[1].NextState)
            //             .GetAccount(ReservedAddresses.LegacyAccount).GetValue),
            //     addresses.Select(
            //         _blockChain.GetWorld(renders[2].Context.PreviousState)
            //             .GetAccount(ReservedAddresses.LegacyAccount).GetValue));
            // Assert.Equal(
            //     new IValue[] { (Text)"foo", (Text)"bar", (Text)"baz", null, (Integer)1 },
            //     addresses.Select(
            //         _blockChain
            //             .GetWorld(renders[2].NextState)
            //             .GetAccount(ReservedAddresses.LegacyAccount)
            //             .GetValue));
            // Assert.Equal("qux", actions[3].Append?.Item);
            // Assert.Equal(2, renders[3].Context.BlockHeight);
            // Assert.Equal(
            //     addresses.Select(
            //         _blockChain
            //             .GetWorld(renders[2].NextState)
            //             .GetAccount(ReservedAddresses.LegacyAccount)
            //             .GetValue),
            //     addresses.Select(
            //         _blockChain
            //             .GetWorld(renders[3].Context.PreviousState)
            //             .GetAccount(ReservedAddresses.LegacyAccount)
            //             .GetValue));
            // Assert.Equal(
            //     new IValue[]
            //     {
            //         (Text)"foo", (Text)"bar", (Text)"baz", (Text)"qux", (Integer)1,
            //     },
            //     addresses.Select(
            //         _blockChain
            //             .GetWorld(renders[3].NextState)
            //             .GetAccount(ReservedAddresses.LegacyAccount)
            //             .GetValue));

            Address minerAddress = addresses[4];
            // RenderRecord.ActionSuccess[] blockRenders = _renderer.ActionSuccessRecords
            //     .Where(r => TestUtils.IsMinerReward(r.Action))
            //     .ToArray();

            Assert.Equal(
                (Integer)2,
                (Integer)_blockChain
                    .GetNextWorld()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(minerAddress));
            // Assert.Equal(2, blockRenders.Length);
            // Assert.True(blockRenders.All(r => r.Render));
            // Assert.Equal(1, blockRenders[0].Context.BlockHeight);
            // Assert.Equal(2, blockRenders[1].Context.BlockHeight);

            // Assert.Equal(
            //     (Integer)1,
            //     (Integer)_blockChain
            //         .GetWorld(blockRenders[0].NextState)
            //         .GetAccount(ReservedAddresses.LegacyAccount)
            //         .GetValue(minerAddress));
            // Assert.Equal(
            //     (Integer)1,
            //     (Integer)_blockChain
            //         .GetWorld(blockRenders[1].Context.PreviousState)
            //         .GetAccount(ReservedAddresses.LegacyAccount)
            //         .GetValue(minerAddress));
            // Assert.Equal(
            //     (Integer)2,
            //     (Integer)_blockChain
            //         .GetWorld(blockRenders[1].NextState)
            //         .GetAccount(ReservedAddresses.LegacyAccount)
            //         .GetValue(minerAddress));

            foreach (Transaction tx in txs)
            {
                Assert.Null(getTxExecution(genesis.BlockHash, tx.Id));
                Assert.Null(getTxExecution(block1.BlockHash, tx.Id));

                TxExecution e = getTxExecution(block2.BlockHash, tx.Id);
                Assert.False(e.Fail);
                Assert.Equal(block2.BlockHash, e.BlockHash);
                Assert.Equal(tx.Id, e.TxId);
            }

            TxExecution txe = getTxExecution(block2.BlockHash, txs[0].Id);
            var outputWorld = _blockChain
                .GetWorld(Assert.IsType<HashDigest<SHA256>>(txe.OutputState));
            Assert.Equal(
                DumbAction.DumbCurrency * 100,
                outputWorld.GetBalance(addresses[0], DumbAction.DumbCurrency));
            Assert.Equal(
                DumbAction.DumbCurrency * 100,
                outputWorld.GetBalance(addresses[1], DumbAction.DumbCurrency));
            Assert.Equal(
                DumbAction.DumbCurrency * 200,
                outputWorld.GetTotalSupply(DumbAction.DumbCurrency));
            txe = getTxExecution(block2.BlockHash, txs[1].Id);
            outputWorld = _blockChain
                .GetWorld(Assert.IsType<HashDigest<SHA256>>(txe.OutputState));
            Assert.Equal(
                DumbAction.DumbCurrency * 100,
                outputWorld.GetBalance(addresses[2], DumbAction.DumbCurrency));
            Assert.Equal(
                DumbAction.DumbCurrency * 100,
                outputWorld.GetBalance(addresses[3], DumbAction.DumbCurrency));
            Assert.Equal(
                DumbAction.DumbCurrency * 400,
                outputWorld.GetTotalSupply(DumbAction.DumbCurrency));

            var pk = new PrivateKey();
            Transaction tx1Transfer = _fx.MakeTransaction(
                new[]
                {
                    DumbAction.Create((pk.Address, "foo"), (addresses[0], addresses[1], 10)),
                    DumbAction.Create((addresses[0], "bar"), (addresses[0], addresses[2], 20)),
                },
                nonce: 0,
                privateKey: pk);
            Transaction tx2Error = _fx.MakeTransaction(
                new[]
                {
                    // As it tries to transfer a negative value, it throws
                    // ArgumentOutOfRangeException:
                    DumbAction.Create((pk.Address, "foo"), (addresses[0], addresses[1], -5)),
                },
                nonce: 1,
                privateKey: pk);
            Transaction tx3Transfer = _fx.MakeTransaction(
                new[]
                {
                    DumbAction.Create((pk.Address, "foo"), (addresses[0], addresses[1], 5)),
                },
                nonce: 2,
                privateKey: pk);
            Block block3 = _blockChain.ProposeBlock(
                keys[4],
                TestUtils.CreateBlockCommit(_blockChain.Tip),
                [tx1Transfer, tx2Error, tx3Transfer],
                []);
            _blockChain.Append(block3, TestUtils.CreateBlockCommit(block3));
            var txExecution1 = getTxExecution(block3.BlockHash, tx1Transfer.Id);
            _logger.Verbose(nameof(txExecution1) + " = {@TxExecution}", txExecution1);
            Assert.False(txExecution1.Fail);
            var inputAccount1 = _blockChain.GetWorld(
                Assert.IsType<HashDigest<SHA256>>(txExecution1.InputState))
                    .GetAccount(ReservedAddresses.LegacyAccount);
            var outputWorld1 = _blockChain.GetWorld(
                Assert.IsType<HashDigest<SHA256>>(txExecution1.OutputState));
            var outputAccount1 = outputWorld1
                    .GetAccount(ReservedAddresses.LegacyAccount);
            var accountDiff1 = AccountDiff.Create(inputAccount1, outputAccount1);

            Assert.Equal(
                (new Address[] { addresses[0], pk.Address }).ToImmutableHashSet(),
                accountDiff1.StateDiffs.Select(kv => kv.Key).ToImmutableHashSet());
            Assert.Equal(
                new Text("foo"),
                outputAccount1.GetValue(pk.Address));
            Assert.Equal(
                new Text("foo,bar"),
                outputAccount1.GetValue(addresses[0]));
            Assert.Equal(
                DumbAction.DumbCurrency * 0,
                outputWorld1.GetBalance(pk.Address, DumbAction.DumbCurrency));
            Assert.Equal(
                DumbAction.DumbCurrency * 70,
                outputWorld1.GetBalance(addresses[0], DumbAction.DumbCurrency));
            Assert.Equal(
                DumbAction.DumbCurrency * 110,
                outputWorld1.GetBalance(addresses[1], DumbAction.DumbCurrency));
            Assert.Equal(
                DumbAction.DumbCurrency * 120,
                outputWorld1.GetBalance(addresses[2], DumbAction.DumbCurrency));

            var txExecution2 = getTxExecution(block3.BlockHash, tx2Error.Id);
            _logger.Verbose(nameof(txExecution2) + " = {@TxExecution}", txExecution2);
            Assert.True(txExecution2.Fail);
            Assert.Equal(block3.BlockHash, txExecution2.BlockHash);
            Assert.Equal(tx2Error.Id, txExecution2.TxId);
            Assert.Contains(
                $"{nameof(System)}.{nameof(ArgumentOutOfRangeException)}",
                txExecution2.ExceptionNames);

            var txExecution3 = getTxExecution(block3.BlockHash, tx3Transfer.Id);
            _logger.Verbose(nameof(txExecution3) + " = {@TxExecution}", txExecution3);
            Assert.False(txExecution3.Fail);
            var outputWorld3 = _blockChain.GetWorld(
                Assert.IsType<HashDigest<SHA256>>(txExecution3.OutputState));
            Assert.Equal(
                DumbAction.DumbCurrency * 0,
                outputWorld3.GetBalance(pk.Address, DumbAction.DumbCurrency));
            Assert.Equal(
                DumbAction.DumbCurrency * 65,
                outputWorld3.GetBalance(addresses[0], DumbAction.DumbCurrency));
            Assert.Equal(
                DumbAction.DumbCurrency * 115,
                outputWorld3.GetBalance(addresses[1], DumbAction.DumbCurrency));
        }

        [SkippableFact]
        public void AppendModern()
        {
            _blockChain = TestUtils.MakeBlockChain();
            var genesis = _blockChain.Genesis;
            var address1 = new Address([.. TestUtils.GetRandomBytes(20)]);
            var address2 = new Address([.. TestUtils.GetRandomBytes(20)]);
            var proposer = new PrivateKey();
            var action1 = DumbModernAction.Create((address1, "foo"));
            var action2 = DumbModernAction.Create((address2, "bar"));
            var tx1 = Transaction.Create(0, proposer, genesis.BlockHash, new[] { action1 }.ToBytecodes());
            var tx2 = Transaction.Create(1, proposer, genesis.BlockHash, new[] { action2 }.ToBytecodes());
            var block1 = _blockChain.ProposeBlock(
                proposer,
                TestUtils.CreateBlockCommit(_blockChain.Tip),
                [tx1],
                []);
            var commit1 = TestUtils.CreateBlockCommit(block1);
            _blockChain.Append(block1, commit1);
            var world1 = _blockChain.GetNextWorld();
            Assert.Equal(
                (Text)"foo",
                world1.GetAccount(DumbModernAction.DumbModernAddress).GetValue(address1));
            var block2 = _blockChain.ProposeBlock(
                proposer,
                commit1,
                [tx2],
                []);
            _blockChain.Append(block2, TestUtils.CreateBlockCommit(block2));
            var world2 = _blockChain.GetNextWorld();
            Assert.Equal(
                (Text)"bar",
                world2.GetAccount(DumbModernAction.DumbModernAddress).GetValue(address2));
        }

        [SkippableFact]
        public void AppendFailDueToInvalidBytesLength()
        {
            DumbAction[] manyActions =
                Enumerable.Repeat(DumbAction.Create((default, "_")), 200).ToArray();
            PrivateKey signer = null;
            int nonce = 0;
            var heavyTxs = new List<Transaction>();
            for (int i = 0; i < 100; i++)
            {
                if (i % 25 == 0)
                {
                    nonce = 0;
                    signer = new PrivateKey();
                }

                Transaction heavyTx = _fx.MakeTransaction(
                    manyActions,
                    nonce: nonce,
                    privateKey: signer);
                heavyTxs.Add(heavyTx);
                nonce += 1;
            }

            var proposer = new PrivateKey();
            var block = _blockChain.ProposeBlock(
                proposer,
                TestUtils.CreateBlockCommit(_blockChain.Tip),
                [.. heavyTxs],
                []);
            long maxBytes = _blockChain.Options.MaxTransactionsBytes;
            Assert.True(ModelSerializer.SerializeToBytes(block).Length > maxBytes);

            var e = Assert.Throws<InvalidOperationException>(() =>
                _blockChain.Append(block, TestUtils.CreateBlockCommit(block)));
        }

        [SkippableFact]
        public void AppendFailDueToInvalidTxCount()
        {
            int nonce = 0;
            int maxTxs = _blockChain.Options.MaxTransactionsPerBlock;
            var manyTxs = new List<Transaction>();
            for (int i = 0; i <= maxTxs; i++)
            {
                Transaction heavyTx = _fx.MakeTransaction(
                    nonce: nonce,
                    privateKey: null);
                manyTxs.Add(heavyTx);
            }

            Assert.True(manyTxs.Count > maxTxs);

            var proposer = new PrivateKey();
            Block block = _blockChain.ProposeBlock(
                proposer,
                TestUtils.CreateBlockCommit(_blockChain.Tip),
                [.. manyTxs],
                []);
            Assert.Equal(manyTxs.Count, block.Transactions.Count);

            var e = Assert.Throws<InvalidOperationException>(() =>
                _blockChain.Append(block, TestUtils.CreateBlockCommit(block)));
        }

        // [SkippableFact]
        // public void AppendWhenActionEvaluationFailed()
        // {
        //     var policy = BlockPolicy.Empty;
        //     var store = new MemoryStore();
        //     var stateStore =
        //         new TrieStateStore();
        //     var actionLoader = new SingleActionLoader<ThrowException>();
        //     var renderer = new RecordingActionRenderer();
        //     BlockChain blockChain =
        //         TestUtils.MakeBlockChain(
        //             policy, store, stateStore, actionLoader, renderers: new[] { renderer });
        //     var privateKey = new PrivateKey();

        //     var action = new ThrowException { ThrowOnExecution = true };
        //     blockChain.MakeTransaction(privateKey, new[] { action });

        //     renderer.ResetRecords();
        //     Block block = blockChain.ProposeBlock(new PrivateKey());
        //     blockChain.Append(block, TestUtils.CreateBlockCommit(block));

        //     Assert.Equal(2, blockChain.Count);
        //     Assert.Empty(renderer.ActionSuccessRecords);
        //     Assert.Single(renderer.ActionErrorRecords);
        //     RenderRecord.ActionError errorRecord = renderer.ActionErrorRecords[0];
        //     Assert.Equal(action.PlainValue, errorRecord.Action);
        //     Assert.IsType<UnexpectedlyTerminatedActionException>(errorRecord.Exception);
        //     Assert.IsType<ThrowException.SomeException>(errorRecord.Exception.InnerException);
        // }

        [SkippableFact]
        public void AppendBlockWithPolicyViolationTx()
        {
            var validKey = new PrivateKey();
            var invalidKey = new PrivateKey();

            void IsSignerValid(BlockChain chain, Transaction tx)
            {
                var validAddress = validKey.Address;
                if (!tx.Signer.Equals(validAddress) && !tx.Signer.Equals(_fx.Proposer.Address))
                {
                    throw new InvalidOperationException("invalid signer");
                }
            }

            var policy = new BlockChainOptions
            {
                TransactionValidation = IsSignerValid,
            };
            using (var fx = new MemoryStoreFixture(policy))
            {
                var blockChain = BlockChain.Create(fx.GenesisBlock, policy);

                var validTx = blockChain.MakeTransaction(validKey, Array.Empty<DumbAction>());
                var invalidTx = blockChain.MakeTransaction(invalidKey, Array.Empty<DumbAction>());

                var proposer = new PrivateKey();

                Block block1 = blockChain.ProposeBlock(
                    proposer,
                    TestUtils.CreateBlockCommit(blockChain.Tip),
                    [validTx],
                    []);
                blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));

                Block block2 = blockChain.ProposeBlock(
                    proposer,
                    TestUtils.CreateBlockCommit(blockChain.Tip),
                    [invalidTx],
                    []);
                Assert.Throws<InvalidOperationException>(() => blockChain.Append(
                    block2, TestUtils.CreateBlockCommit(block2)));
            }
        }

        [SkippableFact]
        public void UnstageAfterAppendComplete()
        {
            PrivateKey privateKey = new PrivateKey();
            (Address[] addresses, Transaction[] txs) =
                MakeFixturesForAppendTests(privateKey, epoch: DateTimeOffset.UtcNow);
            Assert.Empty(_blockChain.GetStagedTransactionIds());

            // Mining with empty staged.
            Block block1 = _blockChain.ProposeBlock(
                privateKey,
                TestUtils.CreateBlockCommit(_blockChain.Tip));
            _blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));
            Assert.Empty(_blockChain.GetStagedTransactionIds());

            StageTransactions(txs);
            Assert.Equal(2, _blockChain.GetStagedTransactionIds().Count);

            // Tx with nonce 0 is mined.
            Block block2 = _blockChain.ProposeBlock(
                privateKey,
                TestUtils.CreateBlockCommit(_blockChain.Tip),
                [txs[0]],
                []);
            _blockChain.Append(block2, TestUtils.CreateBlockCommit(block2));
            Assert.Equal(1, _blockChain.GetStagedTransactionIds().Count);

            // Two txs with nonce 1 are staged.
            var actions = new[] { DumbAction.Create((addresses[0], "foobar")) };
            Transaction[] txs2 =
            {
                _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 1),
            };
            StageTransactions(txs2);
            Assert.Equal(2, _blockChain.GetStagedTransactionIds().Count);

            // Unmined tx is left intact in the stage.
            Block block3 = _blockChain.ProposeBlock(
                privateKey,
                TestUtils.CreateBlockCommit(_blockChain.Tip),
                [txs[1]],
                []);
            _blockChain.Append(block3, TestUtils.CreateBlockCommit(block3));
            Assert.Empty(_blockChain.GetStagedTransactionIds());
            Assert.Empty(_blockChain.StagedTransactions.Iterate(filtered: true));
            Assert.Single(_blockChain.StagedTransactions.Iterate(filtered: false));
        }

        [SkippableFact]
        public void AppendValidatesBlock()
        {
            var options = new BlockChainOptions
            {
                BlockValidation = (_, _) =>
                {
                    throw new InvalidOperationException(string.Empty);
                },
                TransactionValidation = (_, _) =>
                {
                    throw new InvalidOperationException(string.Empty);
                },
            }
                    ;
            var blockChain = new BlockChain(_fx.GenesisBlock, options);
            Assert.Throws<InvalidOperationException>(
                () => blockChain.Append(_fx.Block1, TestUtils.CreateBlockCommit(_fx.Block1)));
        }

        [SkippableFact]
        public void AppendWithdrawTxsWithExpiredNoncesFromStage()
        {
            void AssertTxIdSetEqual(
                IEnumerable<TxId> setOne,
                IEnumerable<TxId> setTwo)
            {
                Assert.Equal(
                    setOne.OrderBy(id => id), setTwo.OrderBy(id => id));
            }

            var signerA = new PrivateKey();
            var signerB = new PrivateKey();
            BlockHash genesis = _blockChain.Genesis.BlockHash;
            Transaction
                txA0 = Transaction.Create(0, signerA, genesis, actions: []),
                txA1 = Transaction.Create(1, signerA, genesis, actions: []);
            _blockChain.StageTransaction(txA0);
            _blockChain.StageTransaction(txA1);
            Block block = _blockChain.ProposeBlock(signerA);

            Transaction
                txA2 = Transaction.Create(2, signerA, genesis, actions: []),
                txA0_ = Transaction.Create(0, signerA, genesis, actions: []),
                txA1_ = Transaction.Create(1, signerA, genesis, actions: []),
                txB0 = Transaction.Create(1, signerB, genesis, actions: []),
                txB1 = Transaction.Create(1, signerB, genesis, actions: []),
                txB2 = Transaction.Create(2, signerB, genesis, actions: []),
                txB0_ = Transaction.Create(1, signerB, genesis, actions: []),
                txB1_ = Transaction.Create(1, signerB, genesis, actions: []);
            _blockChain.StageTransaction(txA2);
            _blockChain.StageTransaction(txA0_);
            _blockChain.StageTransaction(txA1_);
            _blockChain.StageTransaction(txB0);
            _blockChain.StageTransaction(txB1);
            _blockChain.StageTransaction(txB2);
            _blockChain.StageTransaction(txB0_);
            _blockChain.StageTransaction(txB1_);
            AssertTxIdSetEqual(
                new Transaction[]
                {
                    txA0, txA1, txA2, txA0_, txA1_, txB0, txB1, txB2, txB0_, txB1_,
                }.Select(tx => tx.Id).ToImmutableHashSet(),
                _blockChain.GetStagedTransactionIds());

            _blockChain.Append(block, TestUtils.CreateBlockCommit(block));
            AssertTxIdSetEqual(
                new Transaction[]
                {
                    txA2, txB0, txB1, txB2, txB0_, txB1_,
                }.Select(tx => tx.Id).ToImmutableHashSet(),
                _blockChain.GetStagedTransactionIds());
            AssertTxIdSetEqual(
                new Transaction[]
                {
                    txA2, txB0, txB1, txB2, txB0_, txB1_,
                }.Select(tx => tx.Id).ToImmutableHashSet(),
                _blockChain.StagedTransactions.Iterate(filtered: true).Select(tx => tx.Id));
            AssertTxIdSetEqual(
                new Transaction[]
                {
                    txA2, txA0_, txA1_, txB0, txB1, txB2, txB0_, txB1_,
                }.Select(tx => tx.Id).ToImmutableHashSet(),
                _blockChain.StagedTransactions.Iterate(filtered: false).Select(tx => tx.Id));
        }

        [SkippableFact]
        public void DoesNotMigrateStateWithoutAction()
        {
            var policy = new BlockChainOptions
            {
                MaxTransactionsBytes = 50 * 1024,
            };
            var fx = GetStoreFixture(policy);
            // var renderer = new ValidatingActionRenderer();
            var actionEvaluator = new ActionEvaluator(
                stateStore: new TrieStateStore(policy.KeyValueStore),
                policy.PolicyActions);

            var txs = new[]
            {
                Transaction.Create(
                    0,
                    fx.Proposer,
                    default,
                    actions: new[]
                    {
                        new Initialize
                        {
                            Validators = TestUtils.Validators,
                            States = ImmutableDictionary.Create<Address, IValue>(),
                        },
                    }.ToBytecodes(),
                    timestamp: DateTimeOffset.UtcNow),
            };
            var evs = Array.Empty<EvidenceBase>();
            RawBlock preEvalGenesis = RawBlock.Create(
                new BlockHeader
                {
                    Height = 0L,
                    Timestamp = DateTimeOffset.UtcNow,
                    Proposer = fx.Proposer.Address,
                    PreviousHash = default,
                },
                new BlockContent
                {
                    Transactions = [.. txs],
                    Evidences = [.. evs],
                });
            var genesis = preEvalGenesis.Sign(
                fx.Proposer,
                actionEvaluator.Evaluate(preEvalGenesis, default)[^1].OutputState);
            var blockChain = BlockChain.Create(
                options: policy,
                genesisBlock: genesis);
            var emptyBlock = blockChain.ProposeBlock(
                fx.Proposer,
                TestUtils.CreateBlockCommit(blockChain.Tip),
                [],
                []);
            blockChain.Append(emptyBlock, TestUtils.CreateBlockCommit(emptyBlock));
            Assert.Equal<byte>(
                blockChain.GetWorld(genesis.StateRootHash).Trie.Hash.Bytes,
                blockChain.GetNextWorldState(emptyBlock.BlockHash).Trie.Hash.Bytes);
        }

        [Fact]
        public void AppendSRHPostponeBPVBump()
        {
            var beforePostponeBPV = BlockHeader.CurrentProtocolVersion - 1;
            var options = new BlockChainOptions();
            var store = new MemoryStore();
            var stateStore = new TrieStateStore();
            var actionEvaluator = new ActionEvaluator(
                stateStore,
                options.PolicyActions);

            var preGenesis = TestUtils.ProposeGenesis(
                proposer: TestUtils.GenesisProposer.PublicKey,
                protocolVersion: beforePostponeBPV);
            var genesis = preGenesis.Sign(
                TestUtils.GenesisProposer,
                actionEvaluator.Evaluate(preGenesis, default)[^1].OutputState);
            Assert.Equal(beforePostponeBPV, genesis.ProtocolVersion);

            var blockChain = TestUtils.MakeBlockChain(
                options,
                genesisBlock: genesis);

            // Append block before state root hash postpone
            var proposer = new PrivateKey();
            var action = DumbAction.Create((new Address([.. TestUtils.GetRandomBytes(20)]), "foo"));
            var tx = Transaction.Create(0, proposer, genesis.BlockHash, new[] { action }.ToBytecodes());
            var preBlockBeforeBump = TestUtils.ProposeNext(
                genesis,
                [tx],
                proposer.PublicKey,
                protocolVersion: beforePostponeBPV);
            var blockBeforeBump = preBlockBeforeBump.Sign(
                proposer,
                actionEvaluator.Evaluate(
                    preBlockBeforeBump, genesis.StateRootHash)[^1].OutputState);
            Assert.Equal(beforePostponeBPV, blockBeforeBump.ProtocolVersion);
            var commitBeforeBump = TestUtils.CreateBlockCommit(blockBeforeBump);
            blockChain.Append(blockBeforeBump, commitBeforeBump);

            // Append block after state root hash postpone - previous block is not bumped
            action = DumbAction.Create((new Address([.. TestUtils.GetRandomBytes(20)]), "bar"));
            tx = Transaction.Create(1, proposer, genesis.BlockHash, new[] { action }.ToBytecodes());
            var blockAfterBump1 = blockChain.ProposeBlock(
                proposer,
                commitBeforeBump,
                [tx],
                evidences: []);
            Assert.Equal(
                BlockHeader.CurrentProtocolVersion,
                blockAfterBump1.ProtocolVersion);
            var commitAfterBump1 = TestUtils.CreateBlockCommit(blockAfterBump1);
            blockChain.Append(blockAfterBump1, commitAfterBump1);
            Assert.Equal(blockBeforeBump.StateRootHash, blockAfterBump1.StateRootHash);

            // Append block after state root hash postpone - previous block is bumped
            action = DumbAction.Create((new Address([.. TestUtils.GetRandomBytes(20)]), "baz"));
            tx = Transaction.Create(2, proposer, genesis.BlockHash, new[] { action }.ToBytecodes());
            var blockAfterBump2 = blockChain.ProposeBlock(
                proposer,
                commitAfterBump1,
                [tx],
                evidences: []);
            Assert.Equal(
                BlockHeader.CurrentProtocolVersion,
                blockAfterBump2.ProtocolVersion);
            var commitAfterBump2 = TestUtils.CreateBlockCommit(blockAfterBump2);
            blockChain.Append(blockAfterBump2, commitAfterBump2);
            Assert.Equal(
                actionEvaluator.Evaluate(
                    (RawBlock)blockAfterBump1, blockAfterBump1.StateRootHash)[^1].OutputState,
                blockAfterBump2.StateRootHash);
        }
    }
}
