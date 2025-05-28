// using System.Security.Cryptography;
// using Libplanet.State;
// using Libplanet.State;
// using Libplanet.State.Sys;
// using Libplanet.State.Tests.Common;
// using Libplanet;
// using Libplanet.Serialization;
// using Libplanet.Data;
// using Libplanet.Tests.Store;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;

// namespace Libplanet.Tests.Blockchain;

// public partial class BlockChainTest
// {
//     [SkippableTheory]
//     [InlineData(true)]
//     [InlineData(false)]
//     public void Append(bool getTxExecutionViaStore)
//     {
//         // Func<BlockHash, TxId, TxExecution> getTxExecution = new Func<BlockHash, TxId, TxExecution>(
//         //     (BlockHash blockHash, TxId txId) =>
//         //     {
//         //         return _blockChain.TxExecutions[blockHash, txId];
//         //     });

//         PrivateKey[] keys = Enumerable.Repeat(0, 5).Select(_ => new PrivateKey()).ToArray();
//         (Address[] addresses, Transaction[] txs) =
//             MakeFixturesForAppendTests(keys: keys);
//         var genesis = _blockChain.Genesis;

//         Assert.Equal(1, _blockChain.Blocks.Count);
//         // Assert.Empty(_renderer.ActionRecords);
//         // Assert.Empty(_renderer.BlockRecords);
//         var block1 = _blockChain.ProposeBlock(keys[4]);
//         _blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));
//         Assert.NotNull(_blockChain.BlockCommits[block1.BlockHash]);
//         Block block2 = _blockChain.ProposeBlock(keys[4]);
//         foreach (var tx in txs)
//         {
//             Assert.Throws<KeyNotFoundException>(() => _fx.Store.TxExecutions[tx.Id]);
//         }

//         foreach (var tx in txs)
//         {
//             Assert.False(_fx.Store.TxExecutions.ContainsKey(tx.Id));
//         }

//         _blockChain.Append(block2, TestUtils.CreateBlockCommit(block2));

//         foreach (var tx in txs)
//         {
//             Assert.Equal(block2.BlockHash, _fx.Store.TxExecutions[tx.Id].BlockHash);
//         }

//         Assert.True(_blockChain.Blocks.ContainsKey(block2.BlockHash));

//         // RenderRecord.ActionSuccess[] renders = _renderer.ActionSuccessRecords
//         //     .Where(r => TestUtils.IsDumbAction(r.Action))
//         //     .ToArray();
//         // DumbAction[] actions = renders.Select(r => TestUtils.ToDumbAction(r.Action)).ToArray();
//         // Assert.Equal(4, renders.Length);
//         // Assert.True(renders.All(r => r.Render));
//         // Assert.Equal("foo", actions[0].Append?.Item);
//         // Assert.Equal(2, renders[0].Context.BlockHeight);
//         // Assert.Equal(
//         //     new IValue[] { null, null, null, null, (Integer)1 },
//         //     addresses.Select(_blockChain
//         //         .GetWorld(renders[0].Context.PreviousState)
//         //         .GetAccount(ReservedAddresses.LegacyAccount)
//         //         .GetValue));
//         // Assert.Equal(
//         //     new IValue[] { "foo", null, null, null, (Integer)1 },
//         //     addresses.Select(_blockChain
//         //         .GetWorld(renders[0].NextState)
//         //         .GetAccount(ReservedAddresses.LegacyAccount)
//         //         .GetValue));
//         // Assert.Equal("bar", actions[1].Append?.Item);
//         // Assert.Equal(2, renders[1].Context.BlockHeight);
//         // Assert.Equal(
//         //     addresses.Select(_blockChain
//         //         .GetWorld(renders[0].NextState)
//         //         .GetAccount(ReservedAddresses.LegacyAccount)
//         //         .GetValue),
//         //     addresses.Select(_blockChain
//         //         .GetWorld(renders[1].Context.PreviousState)
//         //         .GetAccount(ReservedAddresses.LegacyAccount)
//         //         .GetValue));
//         // Assert.Equal(
//         //     new IValue[] { "foo", "bar", null, null, (Integer)1 },
//         //     addresses.Select(
//         //         _blockChain.GetWorld(renders[1].NextState)
//         //             .GetAccount(ReservedAddresses.LegacyAccount).GetValue));
//         // Assert.Equal("baz", actions[2].Append?.Item);
//         // Assert.Equal(2, renders[2].Context.BlockHeight);
//         // Assert.Equal(
//         //     addresses.Select(
//         //         _blockChain.GetWorld(renders[1].NextState)
//         //             .GetAccount(ReservedAddresses.LegacyAccount).GetValue),
//         //     addresses.Select(
//         //         _blockChain.GetWorld(renders[2].Context.PreviousState)
//         //             .GetAccount(ReservedAddresses.LegacyAccount).GetValue));
//         // Assert.Equal(
//         //     new IValue[] { "foo", "bar", "baz", null, (Integer)1 },
//         //     addresses.Select(
//         //         _blockChain
//         //             .GetWorld(renders[2].NextState)
//         //             .GetAccount(ReservedAddresses.LegacyAccount)
//         //             .GetValue));
//         // Assert.Equal("qux", actions[3].Append?.Item);
//         // Assert.Equal(2, renders[3].Context.BlockHeight);
//         // Assert.Equal(
//         //     addresses.Select(
//         //         _blockChain
//         //             .GetWorld(renders[2].NextState)
//         //             .GetAccount(ReservedAddresses.LegacyAccount)
//         //             .GetValue),
//         //     addresses.Select(
//         //         _blockChain
//         //             .GetWorld(renders[3].Context.PreviousState)
//         //             .GetAccount(ReservedAddresses.LegacyAccount)
//         //             .GetValue));
//         // Assert.Equal(
//         //     new IValue[]
//         //     {
//         //         "foo", "bar", "baz", "qux", (Integer)1,
//         //     },
//         //     addresses.Select(
//         //         _blockChain
//         //             .GetWorld(renders[3].NextState)
//         //             .GetAccount(ReservedAddresses.LegacyAccount)
//         //             .GetValue));

//         Address minerAddress = addresses[4];
//         // RenderRecord.ActionSuccess[] blockRenders = _renderer.ActionSuccessRecords
//         //     .Where(r => TestUtils.IsMinerReward(r.Action))
//         //     .ToArray();

//         Assert.Equal(
//             2,
//             _blockChain
//                 .GetNextWorld()
//                 .GetAccount(ReservedAddresses.LegacyAccount)
//                 .GetValue(minerAddress));
//         // Assert.Equal(2, blockRenders.Length);
//         // Assert.True(blockRenders.All(r => r.Render));
//         // Assert.Equal(1, blockRenders[0].Context.BlockHeight);
//         // Assert.Equal(2, blockRenders[1].Context.BlockHeight);

//         // Assert.Equal(
//         //     (Integer)1,
//         //     (Integer)_blockChain
//         //         .GetWorld(blockRenders[0].NextState)
//         //         .GetAccount(ReservedAddresses.LegacyAccount)
//         //         .GetValue(minerAddress));
//         // Assert.Equal(
//         //     (Integer)1,
//         //     (Integer)_blockChain
//         //         .GetWorld(blockRenders[1].Context.PreviousState)
//         //         .GetAccount(ReservedAddresses.LegacyAccount)
//         //         .GetValue(minerAddress));
//         // Assert.Equal(
//         //     (Integer)2,
//         //     (Integer)_blockChain
//         //         .GetWorld(blockRenders[1].NextState)
//         //         .GetAccount(ReservedAddresses.LegacyAccount)
//         //         .GetValue(minerAddress));

//         foreach (Transaction tx in txs)
//         {
//             Assert.Null(_fx.Store.TxExecutions.GetValueOrDefault(tx.Id, genesis.BlockHash));
//             Assert.Null(_fx.Store.TxExecutions.GetValueOrDefault(tx.Id, block1.BlockHash));

//             TxExecution e = _fx.Store.TxExecutions[tx.Id, block2.BlockHash];
//             Assert.False(e.Fail);
//             Assert.Equal(block2.BlockHash, e.BlockHash);
//             Assert.Equal(tx.Id, e.TxId);
//         }

//         TxExecution txe = _fx.Store.TxExecutions[txs[0].Id, block2.BlockHash];
//         var outputWorld = _blockChain
//             .GetWorld(Assert.IsType<HashDigest<SHA256>>(txe.OutputState));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 100,
//             outputWorld.GetBalance(addresses[0], DumbAction.DumbCurrency));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 100,
//             outputWorld.GetBalance(addresses[1], DumbAction.DumbCurrency));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 200,
//             outputWorld.GetTotalSupply(DumbAction.DumbCurrency));
//         txe = _fx.Store.TxExecutions[txs[1].Id, block2.BlockHash];
//         outputWorld = _blockChain
//             .GetWorld(Assert.IsType<HashDigest<SHA256>>(txe.OutputState));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 100,
//             outputWorld.GetBalance(addresses[2], DumbAction.DumbCurrency));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 100,
//             outputWorld.GetBalance(addresses[3], DumbAction.DumbCurrency));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 400,
//             outputWorld.GetTotalSupply(DumbAction.DumbCurrency));

//         var pk = new PrivateKey();
//         Transaction tx1Transfer = _fx.MakeTransaction(
//             new[]
//             {
//                 DumbAction.Create((pk.Address, "foo"), (addresses[0], addresses[1], 10)),
//                 DumbAction.Create((addresses[0], "bar"), (addresses[0], addresses[2], 20)),
//             },
//             nonce: 0,
//             privateKey: pk);
//         Transaction tx2Error = _fx.MakeTransaction(
//             new[]
//             {
//                 // As it tries to transfer a negative value, it throws
//                 // ArgumentOutOfRangeException:
//                 DumbAction.Create((pk.Address, "foo"), (addresses[0], addresses[1], -5)),
//             },
//             nonce: 1,
//             privateKey: pk);
//         Transaction tx3Transfer = _fx.MakeTransaction(
//             new[]
//             {
//                 DumbAction.Create((pk.Address, "foo"), (addresses[0], addresses[1], 5)),
//             },
//             nonce: 2,
//             privateKey: pk);
//         Block block3 = _blockChain.ProposeBlock(keys[4]);
//         _blockChain.Append(block3, TestUtils.CreateBlockCommit(block3));
//         var txExecution1 = _fx.Store.TxExecutions[tx1Transfer.Id, block3.BlockHash];
//         _logger.Verbose(nameof(txExecution1) + " = {@TxExecution}", txExecution1);
//         Assert.False(txExecution1.Fail);
//         var inputAccount1 = _blockChain.GetWorld(
//             Assert.IsType<HashDigest<SHA256>>(txExecution1.InputState))
//                 .GetAccount(ReservedAddresses.LegacyAccount);
//         var outputWorld1 = _blockChain.GetWorld(
//             Assert.IsType<HashDigest<SHA256>>(txExecution1.OutputState));
//         var outputAccount1 = outputWorld1
//                 .GetAccount(ReservedAddresses.LegacyAccount);
//         var accountDiff1 = AccountDiff.Create(inputAccount1, outputAccount1);

//         Assert.Equal(
//             (new Address[] { addresses[0], pk.Address }).ToImmutableHashSet(),
//             accountDiff1.StateDiffs.Select(kv => kv.Key).ToImmutableHashSet());
//         Assert.Equal(
//             "foo",
//             outputAccount1.GetValue(pk.Address));
//         Assert.Equal(
//             "foo,bar",
//             outputAccount1.GetValue(addresses[0]));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 0,
//             outputWorld1.GetBalance(pk.Address, DumbAction.DumbCurrency));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 70,
//             outputWorld1.GetBalance(addresses[0], DumbAction.DumbCurrency));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 110,
//             outputWorld1.GetBalance(addresses[1], DumbAction.DumbCurrency));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 120,
//             outputWorld1.GetBalance(addresses[2], DumbAction.DumbCurrency));

//         var txExecution2 = _fx.Store.TxExecutions[tx2Error.Id, block3.BlockHash];
//         _logger.Verbose(nameof(txExecution2) + " = {@TxExecution}", txExecution2);
//         Assert.True(txExecution2.Fail);
//         Assert.Equal(block3.BlockHash, txExecution2.BlockHash);
//         Assert.Equal(tx2Error.Id, txExecution2.TxId);
//         Assert.Contains(
//             $"{nameof(System)}.{nameof(ArgumentOutOfRangeException)}",
//             txExecution2.ExceptionNames);

//         var txExecution3 = _fx.Store.TxExecutions[tx3Transfer.Id, block3.BlockHash];
//         _logger.Verbose(nameof(txExecution3) + " = {@TxExecution}", txExecution3);
//         Assert.False(txExecution3.Fail);
//         var outputWorld3 = _blockChain.GetWorld(
//             Assert.IsType<HashDigest<SHA256>>(txExecution3.OutputState));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 0,
//             outputWorld3.GetBalance(pk.Address, DumbAction.DumbCurrency));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 65,
//             outputWorld3.GetBalance(addresses[0], DumbAction.DumbCurrency));
//         Assert.Equal(
//             DumbAction.DumbCurrency * 115,
//             outputWorld3.GetBalance(addresses[1], DumbAction.DumbCurrency));
//     }

//     [Fact]
//     public void AppendModern()
//     {
//         _blockChain = TestUtils.MakeBlockChain();
//         var genesis = _blockChain.Genesis;
//         var address1 = new Address([.. TestUtils.GetRandomBytes(20)]);
//         var address2 = new Address([.. TestUtils.GetRandomBytes(20)]);
//         var proposer = new PrivateKey();
//         var action1 = DumbModernAction.Create((address1, "foo"));
//         var action2 = DumbModernAction.Create((address2, "bar"));
//         var tx1 = new TransactionMetadata
//         {
//             Nonce = 0,
//             Signer = proposer.Address,
//             GenesisHash = genesis.BlockHash,
//             Actions = new[] { action1 }.ToBytecodes(),
//         }.Sign(proposer);
//         var tx2 = new TransactionMetadata
//         {
//             Nonce = 1,
//             Signer = proposer.Address,
//             GenesisHash = genesis.BlockHash,
//             Actions = new[] { action2 }.ToBytecodes(),
//         }.Sign(proposer);
//         var block1 = _blockChain.ProposeBlock(proposer);
//         var commit1 = TestUtils.CreateBlockCommit(block1);
//         _blockChain.Append(block1, commit1);
//         var world1 = _blockChain.GetNextWorld();
//         Assert.Equal(
//             "foo",
//             world1.GetAccount(DumbModernAction.DumbModernAddress).GetValue(address1));
//         var block2 = _blockChain.ProposeBlock(proposer);
//         _blockChain.Append(block2, TestUtils.CreateBlockCommit(block2));
//         var world2 = _blockChain.GetNextWorld();
//         Assert.Equal(
//             "bar",
//             world2.GetAccount(DumbModernAction.DumbModernAddress).GetValue(address2));
//     }

//     [Fact]
//     public void AppendFailDueToInvalidBytesLength()
//     {
//         DumbAction[] manyActions =
//             Enumerable.Repeat(DumbAction.Create((default, "_")), 200).ToArray();
//         PrivateKey signer = null;
//         int nonce = 0;
//         var heavyTxs = new List<Transaction>();
//         for (int i = 0; i < 100; i++)
//         {
//             if (i % 25 == 0)
//             {
//                 nonce = 0;
//                 signer = new PrivateKey();
//             }

//             Transaction heavyTx = _fx.MakeTransaction(
//                 manyActions,
//                 nonce: nonce,
//                 privateKey: signer);
//             heavyTxs.Add(heavyTx);
//             nonce += 1;
//         }

//         var proposer = new PrivateKey();
//         var block = _blockChain.ProposeBlock(proposer);
//         long maxBytes = _blockChain.Options.BlockOptions.MaxTransactionsBytes;
//         Assert.True(ModelSerializer.SerializeToBytes(block).Length > maxBytes);

//         var e = Assert.Throws<InvalidOperationException>(() =>
//             _blockChain.Append(block, TestUtils.CreateBlockCommit(block)));
//     }

//     [Fact]
//     public void AppendFailDueToInvalidTxCount()
//     {
//         int nonce = 0;
//         int maxTxs = _blockChain.Options.BlockOptions.MaxTransactionsPerBlock;
//         var manyTxs = new List<Transaction>();
//         for (int i = 0; i <= maxTxs; i++)
//         {
//             Transaction heavyTx = _fx.MakeTransaction(
//                 nonce: nonce,
//                 privateKey: null);
//             manyTxs.Add(heavyTx);
//         }

//         Assert.True(manyTxs.Count > maxTxs);

//         var proposer = new PrivateKey();
//         Block block = _blockChain.ProposeBlock(proposer);
//         Assert.Equal(manyTxs.Count, block.Transactions.Count);

//         var e = Assert.Throws<InvalidOperationException>(() =>
//             _blockChain.Append(block, TestUtils.CreateBlockCommit(block)));
//     }

//     // [Fact]
//     // public void AppendWhenActionEvaluationFailed()
//     // {
//     //     var policy = BlockPolicy.Empty;
//     //     var store = new Libplanet.Data.Store(new MemoryDatabase());
//     //     var stateStore =
//     //         new TrieStateStore();
//     //     var actionLoader = new SingleActionLoader<ThrowException>();
//     //     var renderer = new RecordingActionRenderer();
//     //     BlockChain blockChain =
//     //         TestUtils.MakeBlockChain(
//     //             policy, store, stateStore, actionLoader, renderers: new[] { renderer });
//     //     var privateKey = new PrivateKey();

//     //     var action = new ThrowException { ThrowOnExecution = true };
//     //     blockChain.MakeTransaction(privateKey, new[] { action });

//     //     renderer.ResetRecords();
//     //     Block block = blockChain.ProposeBlock(new PrivateKey());
//     //     blockChain.Append(block, TestUtils.CreateBlockCommit(block));

//     //     Assert.Equal(2, blockChain.Count);
//     //     Assert.Empty(renderer.ActionSuccessRecords);
//     //     Assert.Single(renderer.ActionErrorRecords);
//     //     RenderRecord.ActionError errorRecord = renderer.ActionErrorRecords[0];
//     //     Assert.Equal(action.PlainValue, errorRecord.Action);
//     //     Assert.IsType<UnexpectedlyTerminatedActionException>(errorRecord.Exception);
//     //     Assert.IsType<ThrowException.SomeException>(errorRecord.Exception.InnerException);
//     // }

//     [Fact]
//     public void AppendBlockWithPolicyViolationTx()
//     {
//         var validKey = new PrivateKey();
//         var invalidKey = new PrivateKey();

//         void IsSignerValid(Transaction tx)
//         {
//             var validAddress = validKey.Address;
//             if (!tx.Signer.Equals(validAddress) && !tx.Signer.Equals(_fx.Proposer.Address))
//             {
//                 throw new InvalidOperationException("invalid signer");
//             }
//         }

//         var policy = new BlockChainOptions
//         {
//             TransactionOptions = new TransactionOptions
//             {
//                 Validator = new RelayValidator<Transaction>(IsSignerValid),
//             },
//         };
//         using (var fx = new MemoryStoreFixture(policy))
//         {
//             var blockChain = new BlockChain(fx.GenesisBlock, policy);

//             var validTx = blockChain.StagedTransactions.Add(new TransactionSubmission
//             {
//                 Signer = validKey,
//             });
//             var invalidTx = blockChain.StagedTransactions.Add(new TransactionSubmission
//             {
//                 Signer = invalidKey,
//             });

//             var proposer = new PrivateKey();

//             Block block1 = blockChain.ProposeBlock(proposer);
//             blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));

//             Block block2 = blockChain.ProposeBlock(proposer);
//             Assert.Throws<InvalidOperationException>(() => blockChain.Append(
//                 block2, TestUtils.CreateBlockCommit(block2)));
//         }
//     }

//     [Fact]
//     public void UnstageAfterAppendComplete()
//     {
//         PrivateKey privateKey = new PrivateKey();
//         (Address[] addresses, Transaction[] txs) =
//             MakeFixturesForAppendTests(privateKey, epoch: DateTimeOffset.UtcNow);
//         Assert.Empty(_blockChain.StagedTransactions.Keys);

//         // Mining with empty staged.
//         Block block1 = _blockChain.ProposeBlock(privateKey);
//         _blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));
//         Assert.Empty(_blockChain.StagedTransactions.Keys);

//         StageTransactions(txs);
//         Assert.Equal(2, _blockChain.StagedTransactions.Keys.Count());

//         // Tx with nonce 0 is mined.
//         Block block2 = _blockChain.ProposeBlock(privateKey);
//         _blockChain.Append(block2, TestUtils.CreateBlockCommit(block2));
//         Assert.Equal(1, _blockChain.StagedTransactions.Keys.Count());

//         // Two txs with nonce 1 are staged.
//         var actions = new[] { DumbAction.Create((addresses[0], "foobar")) };
//         Transaction[] txs2 =
//         {
//             _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 1),
//         };
//         StageTransactions(txs2);
//         Assert.Equal(2, _blockChain.StagedTransactions.Keys.Count());

//         // Unmined tx is left intact in the stage.
//         Block block3 = _blockChain.ProposeBlock(privateKey);
//         _blockChain.Append(block3, TestUtils.CreateBlockCommit(block3));
//         Assert.Empty(_blockChain.StagedTransactions.Keys);
//         Assert.Empty(_blockChain.StagedTransactions.Iterate(filtered: true));
//         Assert.Single(_blockChain.StagedTransactions.Iterate(filtered: false));
//     }

//     [Fact]
//     public void AppendValidatesBlock()
//     {
//         var options = new BlockChainOptions
//         {
//             BlockOptions = new BlockOptions
//             {
//                 Validator = new RelayValidator<Block>(
//                     block =>
//                     {
//                         throw new InvalidOperationException(string.Empty);
//                     }),
//             },
//             TransactionOptions = new TransactionOptions
//             {
//                 Validator = new RelayValidator<Transaction>(
//                     transaction =>
//                     {
//                         throw new InvalidOperationException(string.Empty);
//                     }),
//             },
//         }
//                 ;
//         var blockChain = new BlockChain(_fx.GenesisBlock, options);
//         Assert.Throws<InvalidOperationException>(
//             () => blockChain.Append(_fx.Block1, TestUtils.CreateBlockCommit(_fx.Block1)));
//     }

//     [Fact]
//     public void AppendWithdrawTxsWithExpiredNoncesFromStage()
//     {
//         void AssertTxIdSetEqual(
//             IEnumerable<TxId> setOne,
//             IEnumerable<TxId> setTwo)
//         {
//             Assert.Equal(
//                 setOne.OrderBy(id => id), setTwo.OrderBy(id => id));
//         }

//         var signerA = new PrivateKey();
//         var signerB = new PrivateKey();
//         BlockHash genesis = _blockChain.Genesis.BlockHash;
//         Transaction
//             txA0 = new TransactionMetadata
//             {
//                 Nonce = 0,
//                 Signer = signerA.Address,
//                 GenesisHash = genesis,
//                 Actions = [],
//             }.Sign(signerA),
//             txA1 = new TransactionMetadata
//             {
//                 Nonce = 1,
//                 Signer = signerA.Address,
//                 GenesisHash = genesis,
//                 Actions = [],
//             }.Sign(signerA);
//         _blockChain.StagedTransactions.Add(txA0);
//         _blockChain.StagedTransactions.Add(txA1);
//         Block block = _blockChain.ProposeBlock(signerA);

//         Transaction
//             txA2 = new TransactionMetadata
//             {
//                 Nonce = 2,
//                 Signer = signerA.Address,
//                 GenesisHash = genesis,
//                 Actions = [],
//             }.Sign(signerA),
//             txA0_ = new TransactionMetadata
//             {
//                 Nonce = 0,
//                 Signer = signerA.Address,
//                 GenesisHash = genesis,
//                 Actions = [],
//             }.Sign(signerA),
//             txA1_ = new TransactionMetadata
//             {
//                 Nonce = 1,
//                 Signer = signerA.Address,
//                 GenesisHash = genesis,
//                 Actions = [],
//             }.Sign(signerA),
//             txB0 = new TransactionMetadata
//             {
//                 Nonce = 1,
//                 Signer = signerB.Address,
//                 GenesisHash = genesis,
//                 Actions = [],
//             }.Sign(signerB),
//             txB1 = new TransactionMetadata
//             {
//                 Nonce = 1,
//                 Signer = signerB.Address,
//                 GenesisHash = genesis,
//                 Actions = [],
//             }.Sign(signerB),
//             txB2 = new TransactionMetadata
//             {
//                 Nonce = 2,
//                 Signer = signerB.Address,
//                 GenesisHash = genesis,
//                 Actions = [],
//             }.Sign(signerB),
//             txB0_ = new TransactionMetadata
//             {
//                 Nonce = 1,
//                 Signer = signerB.Address,
//                 GenesisHash = genesis,
//                 Actions = [],
//             }.Sign(signerB),
//             txB1_ = new TransactionMetadata
//             {
//                 Nonce = 1,
//                 Signer = signerB.Address,
//                 GenesisHash = genesis,
//                 Actions = [],
//             }.Sign(signerB);
//         _blockChain.StagedTransactions.Add(txA2);
//         _blockChain.StagedTransactions.Add(txA0_);
//         _blockChain.StagedTransactions.Add(txA1_);
//         _blockChain.StagedTransactions.Add(txB0);
//         _blockChain.StagedTransactions.Add(txB1);
//         _blockChain.StagedTransactions.Add(txB2);
//         _blockChain.StagedTransactions.Add(txB0_);
//         _blockChain.StagedTransactions.Add(txB1_);
//         AssertTxIdSetEqual(
//             new Transaction[]
//             {
//                 txA0, txA1, txA2, txA0_, txA1_, txB0, txB1, txB2, txB0_, txB1_,
//             }.Select(tx => tx.Id).ToImmutableHashSet(),
//             _blockChain.StagedTransactions.Keys);

//         _blockChain.Append(block, TestUtils.CreateBlockCommit(block));
//         AssertTxIdSetEqual(
//             new Transaction[]
//             {
//                 txA2, txB0, txB1, txB2, txB0_, txB1_,
//             }.Select(tx => tx.Id).ToImmutableHashSet(),
//             _blockChain.StagedTransactions.Keys);
//         AssertTxIdSetEqual(
//             new Transaction[]
//             {
//                 txA2, txB0, txB1, txB2, txB0_, txB1_,
//             }.Select(tx => tx.Id).ToImmutableHashSet(),
//             _blockChain.StagedTransactions.Iterate(filtered: true).Select(tx => tx.Id));
//         AssertTxIdSetEqual(
//             new Transaction[]
//             {
//                 txA2, txA0_, txA1_, txB0, txB1, txB2, txB0_, txB1_,
//             }.Select(tx => tx.Id).ToImmutableHashSet(),
//             _blockChain.StagedTransactions.Iterate(filtered: false).Select(tx => tx.Id));
//     }

//     [Fact]
//     public void DoesNotMigrateStateWithoutAction()
//     {
//         var options = new BlockChainOptions
//         {
//             BlockOptions = new BlockOptions
//             {
//                 MaxTransactionsBytes = 50 * 1024,
//             },
//         };
//         var fx = GetStoreFixture(options);
//         // var renderer = new ValidatingActionRenderer();
//         var blockExecutor = new BlockExecutor(
//             stateStore: options.Repository.StateStore,
//             options.PolicyActions);

//         var txs = new[]
//         {
//             new TransactionMetadata
//             {
//                 Nonce = 0,
//                 Signer = fx.Proposer.Address,
//                 Actions = new[]
//                 {
//                     new Initialize
//                     {
//                         Validators = TestUtils.Validators,
//                         States = ImmutableDictionary.Create<Address, object>(),
//                     },
//                 }.ToBytecodes(),
//             }.Sign(fx.Proposer),
//         };
//     var evs = Array.Empty<EvidenceBase>();
//     RawBlock preEvalGenesis = new RawBlock
//     {
//         Header = new BlockHeader
//         {
//             Height = 0,
//             Timestamp = DateTimeOffset.UtcNow,
//             Proposer = fx.Proposer.Address,
//             PreviousHash = default,
//         },
//         Content = new BlockContent
//         {
//             Transactions = [.. txs],
//             Evidences = [.. evs],
//         },
//     };
//     var genesis = preEvalGenesis.Sign(
//         fx.Proposer,
//         blockExecutor.Evaluate(preEvalGenesis, default)[^1].OutputWorld.Trie.Hash);
//     var blockChain = new BlockChain(
//         options: options,
//         genesisBlock: genesis);
//     var emptyBlock = blockChain.ProposeBlock(fx.Proposer);
//     blockChain.Append(emptyBlock, TestUtils.CreateBlockCommit(emptyBlock));
//         Assert.Equal<byte>(
//             blockChain.GetWorld(genesis.StateRootHash).Trie.Hash.Bytes,
//             blockChain.GetNextWorldState(emptyBlock.BlockHash).Trie.Hash.Bytes);
//     }

//     [Fact]
//     public void AppendSRHPostponeBPVBump()
//     {
//         var beforePostponeBPV = BlockHeader.CurrentProtocolVersion - 1;
//         var options = new BlockChainOptions();
//         var store = new Libplanet.Data.Repository(new MemoryDatabase());
//         var stateStore = new TrieStateStore();
//         var blockExecutor = new BlockExecutor(
//             stateStore,
//             options.PolicyActions);

//         var preGenesis = TestUtils.ProposeGenesis(
//             proposer: TestUtils.GenesisProposer.PublicKey,
//             protocolVersion: beforePostponeBPV);
//         var genesis = preGenesis.Sign(
//             TestUtils.GenesisProposer,
//             blockExecutor.Evaluate(preGenesis, default)[^1].OutputWorld.Trie.Hash);
//         Assert.Equal(beforePostponeBPV, genesis.Version);

//         var blockChain = TestUtils.MakeBlockChain(
//             options,
//             genesisBlock: genesis);

//         // Append block before state root hash postpone
//         var proposer = new PrivateKey();
//         var action = DumbAction.Create((new Address([.. TestUtils.GetRandomBytes(20)]), "foo"));
//         var tx = new TransactionMetadata
//         {
//             Nonce = 0,
//             Signer = proposer.Address,
//             GenesisHash = genesis.BlockHash,
//             Actions = new[] { action }.ToBytecodes(),
//         }.Sign(proposer);
//         var preBlockBeforeBump = TestUtils.ProposeNext(
//             genesis,
//             [tx],
//             proposer.PublicKey,
//             protocolVersion: beforePostponeBPV);
//         var blockBeforeBump = preBlockBeforeBump.Sign(
//             proposer,
//             blockExecutor.Evaluate(
//                 preBlockBeforeBump, genesis.StateRootHash)[^1].OutputWorld.Trie.Hash);
//         Assert.Equal(beforePostponeBPV, blockBeforeBump.Version);
//         var commitBeforeBump = TestUtils.CreateBlockCommit(blockBeforeBump);
//         blockChain.Append(blockBeforeBump, commitBeforeBump);

//         // Append block after state root hash postpone - previous block is not bumped
//         action = DumbAction.Create((new Address([.. TestUtils.GetRandomBytes(20)]), "bar"));
//         tx = new TransactionMetadata
//         {
//             Nonce = 1,
//             Signer = proposer.Address,
//             GenesisHash = genesis.BlockHash,
//             Actions = new[] { action }.ToBytecodes(),
//         }.Sign(proposer);
//         var blockAfterBump1 = blockChain.ProposeBlock(proposer);
//         Assert.Equal(
//             BlockHeader.CurrentProtocolVersion,
//             blockAfterBump1.Version);
//         var commitAfterBump1 = TestUtils.CreateBlockCommit(blockAfterBump1);
//         blockChain.Append(blockAfterBump1, commitAfterBump1);
//         Assert.Equal(blockBeforeBump.StateRootHash, blockAfterBump1.StateRootHash);

//         // Append block after state root hash postpone - previous block is bumped
//         action = DumbAction.Create((new Address([.. TestUtils.GetRandomBytes(20)]), "baz"));
//         tx = new TransactionMetadata
//         {
//             Nonce = 2,
//             Signer = proposer.Address,
//             GenesisHash = genesis.BlockHash,
//             Actions = new[] { action }.ToBytecodes(),
//         }.Sign(proposer);
//         var blockAfterBump2 = blockChain.ProposeBlock(proposer);
//         Assert.Equal(
//             BlockHeader.CurrentProtocolVersion,
//             blockAfterBump2.Version);
//         var commitAfterBump2 = TestUtils.CreateBlockCommit(blockAfterBump2);
//         blockChain.Append(blockAfterBump2, commitAfterBump2);
//         Assert.Equal(
//             blockExecutor.Evaluate(
//                 (RawBlock)blockAfterBump1, blockAfterBump1.StateRootHash)[^1].OutputWorld.Trie.Hash,
//             blockAfterBump2.StateRootHash);
//     }
// }
