using System.Security.Cryptography;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet;
using Libplanet.Serialization;
using Libplanet.Data;
using Libplanet.Types;
using Libplanet.TestUtilities;
using System.Security.Cryptography.Pkcs;
using System.Collections.Generic;
using Libplanet.Extensions;

namespace Libplanet.Tests.Blockchain;

public partial class BlockchainTest
{
    [Fact]
    public async Task Append()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Func<BlockHash, TxId, TxExecution> getTxExecution = new Func<BlockHash, TxId, TxExecution>(
        //     (BlockHash blockHash, TxId txId) =>
        //     {
        //         return blockchain.TxExecutions[blockHash, txId];
        //     });

        var random = RandomUtility.GetRandom(_output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 5);
        var addresses = signers.Select(s => s.Address).ToArray();
        var signer = RandomUtility.Signer(random);




        // PrivateKey[] keys = Enumerable.Repeat(0, 5).Select(_ => new PrivateKey()).ToArray();
        // (Address[] addresses, Transaction[] txs) =
        //     MakeFixturesForAppendTests(keys: keys);

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var repository = new Repository();
        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
        };
        var blockchain = new Libplanet.Blockchain(genesisBlock, repository, options);


        Transaction[] txs =
        [
            new TransactionBuilder
            {
                Nonce = 0L,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions =
                [
                    DumbAction.Create((addresses[0], "foo"), (null, addresses[0], 100)),
                    DumbAction.Create((addresses[1], "bar"), (null, addresses[1], 100)),
                ],
            }.Create(signer),
            new TransactionBuilder
            {
                Nonce = 1L,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions =
                [
                    DumbAction.Create((addresses[2], "baz"), (null, addresses[2], 100)),
                    DumbAction.Create((addresses[3], "qux"), (null, addresses[3], 100)),
                ],
            }.Create(signer),
        ];

        // var genesis = blockchain.Genesis;

        Assert.Equal(1, blockchain.Blocks.Count);
        // Assert.Empty(_renderer.ActionRecords);
        // Assert.Empty(_renderer.BlockRecords);
        var block1 = blockchain.ProposeBlock(signers[4]);
        var blockExecuted1Task = blockchain.BlockExecuted.WaitAsync();
        blockchain.Append(block1, TestUtils.CreateBlockCommit(block1));
        var blockExecution1 = await blockExecuted1Task.WaitAsync(cancellationToken);
        Assert.NotNull(blockchain.BlockCommits[block1.BlockHash]);
        blockchain.StagedTransactions.AddRange(txs);
        Block block2 = blockchain.ProposeBlock(signers[4]);
        foreach (var tx in txs)
        {
            Assert.Throws<KeyNotFoundException>(() => repository.TxExecutions[tx.Id]);
        }

        foreach (var tx in txs)
        {
            Assert.False(repository.TxExecutions.ContainsKey(tx.Id));
        }

        var blockExecuted2Task = blockchain.BlockExecuted.WaitAsync();
        blockchain.Append(block2, TestUtils.CreateBlockCommit(block2));
        var blockExecution2 = await blockExecuted2Task.WaitAsync(cancellationToken);

        foreach (var tx in txs)
        {
            Assert.Equal(block2.BlockHash, repository.TxExecutions[tx.Id].BlockHash);
        }

        Assert.True(blockchain.Blocks.ContainsKey(block2.BlockHash));

        // RenderRecord.ActionSuccess[] renders = _renderer.ActionSuccessRecords
        //     .Where(r => TestUtils.IsDumbAction(r.Action))
        //     .ToArray();
        var actions = blockExecution2.Executions
            .SelectMany(e => e.Executions)
            .Select(e => (DumbAction)e.Action)
            .ToArray();
        ActionExecutionInfo[] actionExecutions =
        [
            blockExecution2.Executions[0].Executions[0],
            blockExecution2.Executions[0].Executions[1],
            blockExecution2.Executions[1].Executions[0],
            blockExecution2.Executions[1].Executions[1],
        ];
        Assert.Equal(4, actions.Length);
        // Assert.True(renders.All(r => r.Render));
        Assert.Equal("foo", actions[0].Append?.Item);
        Assert.Equal(2, actionExecutions[0].InputContext.BlockHeight);
        Assert.Equal(
            [null, null, null, null, 1],
            addresses.Select(address => actionExecutions[0].InputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)));
        Assert.Equal(
            ["foo", null, null, null, 1],
            // new IValue[] { "foo", null, null, null, (Integer)1 },
            addresses.Select(address => actionExecutions[0].OutputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)));
        Assert.Equal("bar", actions[1].Append?.Item);
        Assert.Equal(2, actionExecutions[1].InputContext.BlockHeight);
        Assert.Equal(
            addresses.Select(address => actionExecutions[0].OutputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)),
            addresses.Select(address => actionExecutions[1].InputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)));
        Assert.Equal(
            ["foo", "bar", null, null, 1],
            addresses.Select(address => actionExecutions[1].OutputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)));
        Assert.Equal("baz", actions[2].Append?.Item);
        Assert.Equal(2, actionExecutions[2].InputContext.BlockHeight);
        Assert.Equal(
            addresses.Select(address => actionExecutions[1].OutputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)),
            addresses.Select(address => actionExecutions[2].InputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)));
        Assert.Equal(
            ["foo", "bar", "baz", null, 1],
            addresses.Select(address => actionExecutions[2].OutputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)));
        Assert.Equal("qux", actions[3].Append?.Item);
        Assert.Equal(2, actionExecutions[3].InputContext.BlockHeight);
        Assert.Equal(
            addresses.Select(address => actionExecutions[2].OutputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)),
            addresses.Select(address => actionExecutions[3].InputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)));
        Assert.Equal(
            ["foo", "bar", "baz", "qux", 1,],
            addresses.Select(address => actionExecutions[3].OutputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValueOrDefault(address)));

        var minerAddress = addresses[4];
        var rewardMinerActionExecution1 = blockExecution1.EndExecutions.Single();
        var rewardMinerActionExecution2 = blockExecution2.EndExecutions.Single();
        // RenderRecord.ActionSuccess[] blockRenders = _renderer.ActionSuccessRecords
        //     .Where(r => TestUtils.IsMinerReward(r.Action))
        //     .ToArray();

        Assert.Equal(
            2,
            blockchain
                .GetWorld()
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(minerAddress));
        // Assert.Equal(2, blockRenders.Length);
        // Assert.True(blockRenders.All(r => r.Render));
        Assert.Equal(1, rewardMinerActionExecution1.InputContext.BlockHeight);
        Assert.Equal(2, rewardMinerActionExecution2.InputContext.BlockHeight);

        Assert.Equal(
            1,
            rewardMinerActionExecution1.OutputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(minerAddress));
        Assert.Equal(
            1,
            rewardMinerActionExecution2.InputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(minerAddress));
        Assert.Equal(
            2,
            rewardMinerActionExecution2.OutputWorld
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(minerAddress));

        foreach (Transaction tx in txs)
        {
            // Assert.Null(repository.TxExecutions.GetValueOrDefault(tx.Id, genesisBlock.BlockHash));
            // Assert.Null(repository.TxExecutions.GetValueOrDefault(tx.Id, block1.BlockHash));

            TxExecution e = repository.TxExecutions[tx.Id];
            Assert.False(e.Fail);
            Assert.Equal(block2.BlockHash, e.BlockHash);
            Assert.Equal(tx.Id, e.TxId);
        }

        TxExecution txe = repository.TxExecutions[txs[0].Id];
        var outputWorld = blockchain
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
        txe = repository.TxExecutions[txs[1].Id];
        outputWorld = blockchain
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

        var pk = new PrivateKey().AsSigner();
        var tx1Transfer = new TransactionBuilder
        {
            Nonce = 0L,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions =
            [
                DumbAction.Create((pk.Address, "foo"), (addresses[0], addresses[1], 10)),
                DumbAction.Create((addresses[0], "bar"), (addresses[0], addresses[2], 20)),
            ],
        }.Create(pk);
        var tx2Error = new TransactionBuilder
        {
            Nonce = 1L,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions =
            [
                // As it tries to transfer a negative value, it throws
                // ArgumentOutOfRangeException:
                DumbAction.Create((pk.Address, "foo"), (addresses[0], addresses[1], -5)),
            ],
        }.Create(pk);
        var tx3Transfer = new TransactionBuilder
        {
            Nonce = 2L,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions =
            [
                DumbAction.Create((pk.Address, "foo"), (addresses[0], addresses[1], 5)),
            ],
        }.Create(pk);
        blockchain.StagedTransactions.AddRange([tx1Transfer, tx2Error, tx3Transfer]);
        Block block3 = blockchain.ProposeBlock(signers[4]);
        blockchain.Append(block3, TestUtils.CreateBlockCommit(block3));
        var txExecution1 = repository.TxExecutions[tx1Transfer.Id];
        // _logger.Verbose(nameof(txExecution1) + " = {@TxExecution}", txExecution1);
        Assert.False(txExecution1.Fail);
        var inputAccount1 = blockchain.GetWorld(
            Assert.IsType<HashDigest<SHA256>>(txExecution1.InputState))
                .GetAccount(SystemAddresses.SystemAccount);
        var outputWorld1 = blockchain.GetWorld(
            Assert.IsType<HashDigest<SHA256>>(txExecution1.OutputState));
        var outputAccount1 = outputWorld1
                .GetAccount(SystemAddresses.SystemAccount);
        // var accountDiff1 = AccountDiff.Create(inputAccount1, outputAccount1);

        // Assert.Equal(
        //     (new Address[] { addresses[0], pk.Address }).ToImmutableHashSet(),
        //     accountDiff1.StateDiffs.Select(kv => kv.Key).ToImmutableHashSet());
        Assert.Equal(
            "foo",
            outputAccount1.GetValue(pk.Address));
        Assert.Equal(
            "foo,bar",
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

        var txExecution2 = repository.TxExecutions[tx2Error.Id];
        // _logger.Verbose(nameof(txExecution2) + " = {@TxExecution}", txExecution2);
        Assert.True(txExecution2.Fail);
        Assert.Equal(block3.BlockHash, txExecution2.BlockHash);
        Assert.Equal(tx2Error.Id, txExecution2.TxId);
        Assert.Contains(
            $"{nameof(System)}.{nameof(ArgumentOutOfRangeException)}",
            txExecution2.ExceptionNames);

        var txExecution3 = repository.TxExecutions[tx3Transfer.Id];
        // _logger.Verbose(nameof(txExecution3) + " = {@TxExecution}", txExecution3);
        Assert.False(txExecution3.Fail);
        var outputWorld3 = blockchain.GetWorld(
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

    // [Fact]
    // public void AppendModern()
    // {
    //     blockchain = TestUtils.MakeBlockChain();
    //     var genesis = blockchain.Genesis;
    //     var address1 = new Address([.. RandomUtility.Bytes(20)]);
    //     var address2 = new Address([.. RandomUtility.Bytes(20)]);
    //     var proposer = new PrivateKey();
    //     var action1 = DumbModernAction.Create((address1, "foo"));
    //     var action2 = DumbModernAction.Create((address2, "bar"));
    //     var tx1 = new TransactionMetadata
    //     {
    //         Nonce = 0,
    //         Signer = proposer.Address,
    //         GenesisHash = genesis.BlockHash,
    //         Actions = new[] { action1 }.ToBytecodes(),
    //     }.Sign(proposer);
    //     var tx2 = new TransactionMetadata
    //     {
    //         Nonce = 1,
    //         Signer = proposer.Address,
    //         GenesisHash = genesis.BlockHash,
    //         Actions = new[] { action2 }.ToBytecodes(),
    //     }.Sign(proposer);
    //     var block1 = blockchain.ProposeBlock(proposer);
    //     var commit1 = TestUtils.CreateBlockCommit(block1);
    //     blockchain.Append(block1, commit1);
    //     var world1 = blockchain.GetWorld();
    //     Assert.Equal(
    //         "foo",
    //         world1.GetAccount(DumbModernAction.DumbModernAddress).GetValue(address1));
    //     var block2 = blockchain.ProposeBlock(proposer);
    //     blockchain.Append(block2, TestUtils.CreateBlockCommit(block2));
    //     var world2 = blockchain.GetWorld();
    //     Assert.Equal(
    //         "bar",
    //         world2.GetAccount(DumbModernAction.DumbModernAddress).GetValue(address2));
    // }

    // [Fact]
    // public void AppendFailDueToInvalidBytesLength()
    // {
    //     DumbAction[] manyActions =
    //         Enumerable.Repeat(DumbAction.Create((default, "_")), 200).ToArray();
    //     PrivateKey signer = null;
    //     int nonce = 0;
    //     var heavyTxs = new List<Transaction>();
    //     for (int i = 0; i < 100; i++)
    //     {
    //         if (i % 25 == 0)
    //         {
    //             nonce = 0;
    //             signer = new PrivateKey();
    //         }

    //         Transaction heavyTx = _fx.MakeTransaction(
    //             manyActions,
    //             nonce: nonce,
    //             privateKey: signer);
    //         heavyTxs.Add(heavyTx);
    //         nonce += 1;
    //     }

    //     var proposer = new PrivateKey();
    //     var block = blockchain.ProposeBlock(proposer);
    //     long maxBytes = blockchain.Options.BlockOptions.MaxTransactionsBytes;
    //     Assert.True(ModelSerializer.SerializeToBytes(block).Length > maxBytes);

    //     var e = Assert.Throws<InvalidOperationException>(() =>
    //         blockchain.Append(block, TestUtils.CreateBlockCommit(block)));
    // }

    // [Fact]
    // public void AppendFailDueToInvalidTxCount()
    // {
    //     int nonce = 0;
    //     int maxTxs = blockchain.Options.BlockOptions.MaxTransactionsPerBlock;
    //     var manyTxs = new List<Transaction>();
    //     for (int i = 0; i <= maxTxs; i++)
    //     {
    //         Transaction heavyTx = _fx.MakeTransaction(
    //             nonce: nonce,
    //             privateKey: null);
    //         manyTxs.Add(heavyTx);
    //     }

    //     Assert.True(manyTxs.Count > maxTxs);

    //     var proposer = new PrivateKey();
    //     Block block = blockchain.ProposeBlock(proposer);
    //     Assert.Equal(manyTxs.Count, block.Transactions.Count);

    //     var e = Assert.Throws<InvalidOperationException>(() =>
    //         blockchain.Append(block, TestUtils.CreateBlockCommit(block)));
    // }

    // // [Fact]
    // // public void AppendWhenActionEvaluationFailed()
    // // {
    // //     var policy = BlockPolicy.Empty;
    // //     var store = new Libplanet.Data.Store(new MemoryDatabase());
    // //     var stateStore =
    // //         new TrieStateStore();
    // //     var actionLoader = new SingleActionLoader<ThrowException>();
    // //     var renderer = new RecordingActionRenderer();
    // //     BlockChain blockChain =
    // //         TestUtils.MakeBlockChain(
    // //             policy, store, stateStore, actionLoader, renderers: new[] { renderer });
    // //     var privateKey = new PrivateKey();

    // //     var action = new ThrowException { ThrowOnExecution = true };
    // //     blockChain.MakeTransaction(privateKey, new[] { action });

    // //     renderer.ResetRecords();
    // //     Block block = blockChain.ProposeBlock(new PrivateKey());
    // //     blockChain.Append(block, TestUtils.CreateBlockCommit(block));

    // //     Assert.Equal(2, blockChain.Count);
    // //     Assert.Empty(renderer.ActionSuccessRecords);
    // //     Assert.Single(renderer.ActionErrorRecords);
    // //     RenderRecord.ActionError errorRecord = renderer.ActionErrorRecords[0];
    // //     Assert.Equal(action.PlainValue, errorRecord.Action);
    // //     Assert.IsType<UnexpectedlyTerminatedActionException>(errorRecord.Exception);
    // //     Assert.IsType<ThrowException.SomeException>(errorRecord.Exception.InnerException);
    // // }

    // [Fact]
    // public void AppendBlockWithPolicyViolationTx()
    // {
    //     var validKey = new PrivateKey();
    //     var invalidKey = new PrivateKey();

    //     void IsSignerValid(Transaction tx)
    //     {
    //         var validAddress = validKey.Address;
    //         if (!tx.Signer.Equals(validAddress) && !tx.Signer.Equals(_fx.Proposer.Address))
    //         {
    //             throw new InvalidOperationException("invalid signer");
    //         }
    //     }

    //     var policy = new BlockChainOptions
    //     {
    //         TransactionOptions = new TransactionOptions
    //         {
    //             Validator = new RelayValidator<Transaction>(IsSignerValid),
    //         },
    //     };
    //     using (var fx = new MemoryStoreFixture(policy))
    //     {
    //         var blockChain = new BlockChain(fx.GenesisBlock, policy);

    //         var validTx = blockChain.StagedTransactions.Add(new TransactionSubmission
    //         {
    //             Signer = validKey,
    //         });
    //         var invalidTx = blockChain.StagedTransactions.Add(new TransactionSubmission
    //         {
    //             Signer = invalidKey,
    //         });

    //         var proposer = new PrivateKey();

    //         Block block1 = blockChain.ProposeBlock(proposer);
    //         blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));

    //         Block block2 = blockChain.ProposeBlock(proposer);
    //         Assert.Throws<InvalidOperationException>(() => blockChain.Append(
    //             block2, TestUtils.CreateBlockCommit(block2)));
    //     }
    // }

    // [Fact]
    // public void UnstageAfterAppendComplete()
    // {
    //     PrivateKey privateKey = new PrivateKey();
    //     (Address[] addresses, Transaction[] txs) =
    //         MakeFixturesForAppendTests(privateKey, epoch: DateTimeOffset.UtcNow);
    //     Assert.Empty(blockchain.StagedTransactions.Keys);

    //     // Mining with empty staged.
    //     Block block1 = blockchain.ProposeBlock(privateKey);
    //     blockchain.Append(block1, TestUtils.CreateBlockCommit(block1));
    //     Assert.Empty(blockchain.StagedTransactions.Keys);

    //     StageTransactions(txs);
    //     Assert.Equal(2, blockchain.StagedTransactions.Keys.Count());

    //     // Tx with nonce 0 is mined.
    //     Block block2 = blockchain.ProposeBlock(privateKey);
    //     blockchain.Append(block2, TestUtils.CreateBlockCommit(block2));
    //     Assert.Equal(1, blockchain.StagedTransactions.Keys.Count());

    //     // Two txs with nonce 1 are staged.
    //     var actions = new[] { DumbAction.Create((addresses[0], "foobar")) };
    //     Transaction[] txs2 =
    //     {
    //         _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 1),
    //     };
    //     StageTransactions(txs2);
    //     Assert.Equal(2, blockchain.StagedTransactions.Keys.Count());

    //     // Unmined tx is left intact in the stage.
    //     Block block3 = blockchain.ProposeBlock(privateKey);
    //     blockchain.Append(block3, TestUtils.CreateBlockCommit(block3));
    //     Assert.Empty(blockchain.StagedTransactions.Keys);
    //     Assert.Empty(blockchain.StagedTransactions.Iterate(filtered: true));
    //     Assert.Single(blockchain.StagedTransactions.Iterate(filtered: false));
    // }

    // [Fact]
    // public void AppendValidatesBlock()
    // {
    //     var options = new BlockChainOptions
    //     {
    //         BlockOptions = new BlockOptions
    //         {
    //             Validator = new RelayValidator<Block>(
    //                 block =>
    //                 {
    //                     throw new InvalidOperationException(string.Empty);
    //                 }),
    //         },
    //         TransactionOptions = new TransactionOptions
    //         {
    //             Validator = new RelayValidator<Transaction>(
    //                 transaction =>
    //                 {
    //                     throw new InvalidOperationException(string.Empty);
    //                 }),
    //         },
    //     }
    //             ;
    //     var blockChain = new BlockChain(_fx.GenesisBlock, options);
    //     Assert.Throws<InvalidOperationException>(
    //         () => blockChain.Append(_fx.Block1, TestUtils.CreateBlockCommit(_fx.Block1)));
    // }

    // [Fact]
    // public void AppendWithdrawTxsWithExpiredNoncesFromStage()
    // {
    //     void AssertTxIdSetEqual(
    //         IEnumerable<TxId> setOne,
    //         IEnumerable<TxId> setTwo)
    //     {
    //         Assert.Equal(
    //             setOne.OrderBy(id => id), setTwo.OrderBy(id => id));
    //     }

    //     var signerA = new PrivateKey();
    //     var signerB = new PrivateKey();
    //     BlockHash genesis = blockchain.Genesis.BlockHash;
    //     Transaction
    //         txA0 = new TransactionMetadata
    //         {
    //             Nonce = 0,
    //             Signer = signerA.Address,
    //             GenesisHash = genesis,
    //             Actions = [],
    //         }.Sign(signerA),
    //         txA1 = new TransactionMetadata
    //         {
    //             Nonce = 1,
    //             Signer = signerA.Address,
    //             GenesisHash = genesis,
    //             Actions = [],
    //         }.Sign(signerA);
    //     blockchain.StagedTransactions.Add(txA0);
    //     blockchain.StagedTransactions.Add(txA1);
    //     Block block = blockchain.ProposeBlock(signerA);

    //     Transaction
    //         txA2 = new TransactionMetadata
    //         {
    //             Nonce = 2,
    //             Signer = signerA.Address,
    //             GenesisHash = genesis,
    //             Actions = [],
    //         }.Sign(signerA),
    //         txA0_ = new TransactionMetadata
    //         {
    //             Nonce = 0,
    //             Signer = signerA.Address,
    //             GenesisHash = genesis,
    //             Actions = [],
    //         }.Sign(signerA),
    //         txA1_ = new TransactionMetadata
    //         {
    //             Nonce = 1,
    //             Signer = signerA.Address,
    //             GenesisHash = genesis,
    //             Actions = [],
    //         }.Sign(signerA),
    //         txB0 = new TransactionMetadata
    //         {
    //             Nonce = 1,
    //             Signer = signerB.Address,
    //             GenesisHash = genesis,
    //             Actions = [],
    //         }.Sign(signerB),
    //         txB1 = new TransactionMetadata
    //         {
    //             Nonce = 1,
    //             Signer = signerB.Address,
    //             GenesisHash = genesis,
    //             Actions = [],
    //         }.Sign(signerB),
    //         txB2 = new TransactionMetadata
    //         {
    //             Nonce = 2,
    //             Signer = signerB.Address,
    //             GenesisHash = genesis,
    //             Actions = [],
    //         }.Sign(signerB),
    //         txB0_ = new TransactionMetadata
    //         {
    //             Nonce = 1,
    //             Signer = signerB.Address,
    //             GenesisHash = genesis,
    //             Actions = [],
    //         }.Sign(signerB),
    //         txB1_ = new TransactionMetadata
    //         {
    //             Nonce = 1,
    //             Signer = signerB.Address,
    //             GenesisHash = genesis,
    //             Actions = [],
    //         }.Sign(signerB);
    //     blockchain.StagedTransactions.Add(txA2);
    //     blockchain.StagedTransactions.Add(txA0_);
    //     blockchain.StagedTransactions.Add(txA1_);
    //     blockchain.StagedTransactions.Add(txB0);
    //     blockchain.StagedTransactions.Add(txB1);
    //     blockchain.StagedTransactions.Add(txB2);
    //     blockchain.StagedTransactions.Add(txB0_);
    //     blockchain.StagedTransactions.Add(txB1_);
    //     AssertTxIdSetEqual(
    //         new Transaction[]
    //         {
    //             txA0, txA1, txA2, txA0_, txA1_, txB0, txB1, txB2, txB0_, txB1_,
    //         }.Select(tx => tx.Id).ToImmutableHashSet(),
    //         blockchain.StagedTransactions.Keys);

    //     blockchain.Append(block, TestUtils.CreateBlockCommit(block));
    //     AssertTxIdSetEqual(
    //         new Transaction[]
    //         {
    //             txA2, txB0, txB1, txB2, txB0_, txB1_,
    //         }.Select(tx => tx.Id).ToImmutableHashSet(),
    //         blockchain.StagedTransactions.Keys);
    //     AssertTxIdSetEqual(
    //         new Transaction[]
    //         {
    //             txA2, txB0, txB1, txB2, txB0_, txB1_,
    //         }.Select(tx => tx.Id).ToImmutableHashSet(),
    //         blockchain.StagedTransactions.Iterate(filtered: true).Select(tx => tx.Id));
    //     AssertTxIdSetEqual(
    //         new Transaction[]
    //         {
    //             txA2, txA0_, txA1_, txB0, txB1, txB2, txB0_, txB1_,
    //         }.Select(tx => tx.Id).ToImmutableHashSet(),
    //         blockchain.StagedTransactions.Iterate(filtered: false).Select(tx => tx.Id));
    // }

    // [Fact]
    // public void DoesNotMigrateStateWithoutAction()
    // {
    //     var options = new BlockChainOptions
    //     {
    //         BlockOptions = new BlockOptions
    //         {
    //             MaxTransactionsBytes = 50 * 1024,
    //         },
    //     };
    //     var fx = GetStoreFixture(options);
    //     // var renderer = new ValidatingActionRenderer();
    //     var blockExecutor = new BlockExecutor(
    //         stateStore: options.Repository.StateStore,
    //         options.PolicyActions);

    //     var txs = new[]
    //     {
    //         new TransactionMetadata
    //         {
    //             Nonce = 0,
    //             Signer = fx.Proposer.Address,
    //             Actions = new[]
    //             {
    //                 new Initialize
    //                 {
    //                     Validators = TestUtils.Validators,
    //                     States = ImmutableDictionary.Create<Address, object>(),
    //                 },
    //             }.ToBytecodes(),
    //         }.Sign(fx.Proposer),
    //     };
    // var evs = Array.Empty<EvidenceBase>();
    // RawBlock preEvalGenesis = new RawBlock
    // {
    //     Header = new BlockHeader
    //     {
    //         Height = 0,
    //         Timestamp = DateTimeOffset.UtcNow,
    //         Proposer = fx.Proposer.Address,
    //         PreviousHash = default,
    //     },
    //     Content = new BlockContent
    //     {
    //         Transactions = [.. txs],
    //         Evidences = [.. evs],
    //     },
    // };
    // var genesis = preEvalGenesis.Sign(
    //     fx.Proposer,
    //     blockExecutor.Evaluate(preEvalGenesis, default)[^1].OutputWorld.Trie.Hash);
    // var blockChain = new BlockChain(
    //     options: options,
    //     genesisBlock: genesis);
    // var emptyBlock = blockChain.ProposeBlock(fx.Proposer);
    // blockChain.Append(emptyBlock, TestUtils.CreateBlockCommit(emptyBlock));
    //     Assert.Equal<byte>(
    //         blockChain.GetWorld(genesis.StateRootHash).Trie.Hash.Bytes,
    //         blockChain.GetWorldState(emptyBlock.BlockHash).Trie.Hash.Bytes);
    // }

    // [Fact]
    // public void AppendSRHPostponeBPVBump()
    // {
    //     var beforePostponeBPV = BlockHeader.CurrentProtocolVersion - 1;
    //     var options = new BlockChainOptions();
    //     var store = new Libplanet.Data.Repository(new MemoryDatabase());
    //     var stateStore = new TrieStateStore();
    //     var blockExecutor = new BlockExecutor(
    //         stateStore,
    //         options.PolicyActions);

    //     var preGenesis = TestUtils.ProposeGenesis(
    //         proposer: TestUtils.GenesisProposer.PublicKey,
    //         protocolVersion: beforePostponeBPV);
    //     var genesis = preGenesis.Sign(
    //         TestUtils.GenesisProposer,
    //         blockExecutor.Evaluate(preGenesis, default)[^1].OutputWorld.Trie.Hash);
    //     Assert.Equal(beforePostponeBPV, genesis.Version);

    //     var blockChain = TestUtils.MakeBlockChain(
    //         options,
    //         genesisBlock: genesis);

    //     // Append block before state root hash postpone
    //     var proposer = new PrivateKey();
    //     var action = DumbAction.Create((new Address([.. RandomUtility.Bytes(20)]), "foo"));
    //     var tx = new TransactionMetadata
    //     {
    //         Nonce = 0,
    //         Signer = proposer.Address,
    //         GenesisHash = genesis.BlockHash,
    //         Actions = new[] { action }.ToBytecodes(),
    //     }.Sign(proposer);
    //     var preBlockBeforeBump = TestUtils.ProposeNext(
    //         genesis,
    //         [tx],
    //         proposer.PublicKey,
    //         protocolVersion: beforePostponeBPV);
    //     var blockBeforeBump = preBlockBeforeBump.Sign(
    //         proposer,
    //         blockExecutor.Evaluate(
    //             preBlockBeforeBump, genesis.StateRootHash)[^1].OutputWorld.Trie.Hash);
    //     Assert.Equal(beforePostponeBPV, blockBeforeBump.Version);
    //     var commitBeforeBump = TestUtils.CreateBlockCommit(blockBeforeBump);
    //     blockChain.Append(blockBeforeBump, commitBeforeBump);

    //     // Append block after state root hash postpone - previous block is not bumped
    //     action = DumbAction.Create((new Address([.. RandomUtility.Bytes(20)]), "bar"));
    //     tx = new TransactionMetadata
    //     {
    //         Nonce = 1,
    //         Signer = proposer.Address,
    //         GenesisHash = genesis.BlockHash,
    //         Actions = new[] { action }.ToBytecodes(),
    //     }.Sign(proposer);
    //     var blockAfterBump1 = blockChain.ProposeBlock(proposer);
    //     Assert.Equal(
    //         BlockHeader.CurrentProtocolVersion,
    //         blockAfterBump1.Version);
    //     var commitAfterBump1 = TestUtils.CreateBlockCommit(blockAfterBump1);
    //     blockChain.Append(blockAfterBump1, commitAfterBump1);
    //     Assert.Equal(blockBeforeBump.StateRootHash, blockAfterBump1.StateRootHash);

    //     // Append block after state root hash postpone - previous block is bumped
    //     action = DumbAction.Create((new Address([.. RandomUtility.Bytes(20)]), "baz"));
    //     tx = new TransactionMetadata
    //     {
    //         Nonce = 2,
    //         Signer = proposer.Address,
    //         GenesisHash = genesis.BlockHash,
    //         Actions = new[] { action }.ToBytecodes(),
    //     }.Sign(proposer);
    //     var blockAfterBump2 = blockChain.ProposeBlock(proposer);
    //     Assert.Equal(
    //         BlockHeader.CurrentProtocolVersion,
    //         blockAfterBump2.Version);
    //     var commitAfterBump2 = TestUtils.CreateBlockCommit(blockAfterBump2);
    //     blockChain.Append(blockAfterBump2, commitAfterBump2);
    //     Assert.Equal(
    //         blockExecutor.Evaluate(
    //             (RawBlock)blockAfterBump1, blockAfterBump1.StateRootHash)[^1].OutputWorld.Trie.Hash,
    //         blockAfterBump2.StateRootHash);
    // }
}
