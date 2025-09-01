using Libplanet.Data;
using Libplanet.Extensions;
using Libplanet.Serialization;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Xunit.Internal;
using static Libplanet.State.SystemAddresses;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Action;

public partial class BlockExecutorTest(ITestOutputHelper output)
{
    [Fact]
    public void Idempotent()
    {
        // NOTE: This test checks that blocks can be evaluated idempotently. Also it checks
        // the action results in pre-evaluation step and in evaluation step are equal.
        const int repeatCount = 2;
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var timestamp = DateTimeOffset.UtcNow;
        var txAddress = signer.Address;
        var txs = new[]
        {
            new TransactionBuilder
            {
                Actions = [new ContextRecording { Address = txAddress, Value = "Foo" }],
            }.Create(signer),
        };
        var evs = Array.Empty<EvidenceBase>();
        var stateIndex = new StateIndex();
        var blockExecutor = new BlockExecutor(stateIndex);
        var rawBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = BlockHeader.CurrentProtocolVersion,
                Height = 0,
                Timestamp = timestamp,
                Proposer = proposer.Address,
            },
            Content = new BlockContent
            {
                Transactions = [.. txs],
                Evidences = [.. evs],
            },
        };
        var block = rawBlock.Sign(proposer);

        Assert.Equal(rawBlock, (RawBlock)block);

        for (var i = 0; i < repeatCount; ++i)
        {
            var blockExecution1 = blockExecutor.Execute(block);
            var world1 = new World(blockExecution1.LeaveWorld.Trie, stateIndex);
            var value1 = world1.GetAccount(SystemAccount).GetValue(ContextRecording.RandomRecordAddress);
            var blockExecution2 = blockExecutor.Execute(block);
            var world2 = new World(blockExecution2.LeaveWorld.Trie, stateIndex);
            var value2 = world2.GetAccount(SystemAccount).GetValue(ContextRecording.RandomRecordAddress);
            Assert.Equal(value1, value2);
        }
    }

    [Fact]
    public async Task Evaluate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var signer = RandomUtility.Signer(random);

        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var repositoryA = new Repository();
        var blockchain = new Blockchain(genesisBlock, repositoryA);
        var action = new ContextRecording { Address = signer.Address, Value = "Foo" };

        var repositoryB = new Repository();
        var blockExecutor = new BlockExecutor(repositoryB.States);
        await repositoryA.CopyToAsync(repositoryB, cancellationToken);

        _ = blockchain.StagedTransactions.Add(signer, new()
        {
            Actions = [action],
        });
        blockchain.ProposeAndAppend(proposer);

        var executionInfo = blockExecutor.Execute(blockchain.Tip);
        var txExecutionInfo = Assert.Single(executionInfo.Executions);
        var actionExecutionInfo = Assert.Single(txExecutionInfo.Executions);
        var world = new World(executionInfo.LeaveWorld.Trie, repositoryA.States);
        Assert.Equal(blockchain.StateRootHash, executionInfo.StateRootHash);
        Assert.Null(actionExecutionInfo.Exception);
        Assert.Equal("Foo", blockchain.GetSystemValue(signer.Address));
        Assert.Equal("Foo", world.GetSystemValue(signer.Address));
        Assert.Equal(
            proposer.Address,
            blockchain.GetSystemValue(ContextRecording.MinerRecordAddress));
        Assert.Equal(
            proposer.Address,
            world.GetSystemValue(ContextRecording.MinerRecordAddress));
        Assert.Equal(
            signer.Address,
            blockchain.GetSystemValue(ContextRecording.SignerRecordAddress));
        Assert.Equal(
            signer.Address,
            world.GetSystemValue(ContextRecording.SignerRecordAddress));
        Assert.Equal(
            blockchain.Tip.Height,
            blockchain.GetSystemValue(ContextRecording.BlockIndexRecordAddress));
        Assert.Equal(
            blockchain.Tip.Height,
            world.GetSystemValue(ContextRecording.BlockIndexRecordAddress));
        Assert.Equal(
            actionExecutionInfo.ActionContext.GetRandom().Next(),
            blockchain.GetSystemValue(ContextRecording.RandomRecordAddress));
        Assert.Equal(
            actionExecutionInfo.ActionContext.GetRandom().Next(),
            world.GetSystemValue(ContextRecording.RandomRecordAddress));
    }

    [Fact]
    public async Task EvaluateWithSystemActions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var repositoryB = new Repository();
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 4);
        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EnterBlockActions = [new UpdateValue { Address = signers[0].Address, Increment = 1 }],
                LeaveBlockActions = [new UpdateValue { Address = signers[1].Address, Increment = 1 }],
                EnterTxActions = [new UpdateValue { Address = signers[2].Address, Increment = 1 }],
                LeaveTxActions = [new UpdateValue { Address = signers[3].Address, Increment = 1 }],
            },
        };
        var blockchain = new Blockchain(genesisBlock, repositoryB, options);

        Assert.Equal((BigInteger)1, blockchain.GetSystemValue(signers[0].Address));
        Assert.Equal((BigInteger)1, blockchain.GetSystemValue(signers[1].Address));
        Assert.Equal((BigInteger)genesisBlock.Transactions.Count, blockchain.GetSystemValue(signers[2].Address));
        Assert.Equal((BigInteger)genesisBlock.Transactions.Count, blockchain.GetSystemValue(signers[3].Address));

        blockchain.StagedTransactions.Add(proposer, @params: new()
        {
            Actions =
            [
                DumbAction.Create((signers[0].Address, "foo")),
                DumbAction.Create((signers[1].Address, "bar")),
            ],
        });
        blockchain.StagedTransactions.Add(proposer, @params: new()
        {
            Actions =
            [
                DumbAction.Create((signers[2].Address, "baz")),
                DumbAction.Create((signers[3].Address, "qux")),
            ],
        });

        var block = blockchain.Propose(proposer);
        var blockCommit = CreateBlockCommit(block);

        var repositoryA = new Repository();

        await repositoryB.CopyToAsync(repositoryA, cancellationToken);

        var blockExecutor = new BlockExecutor(repositoryA.States, options.SystemActions);
        var executionInfoA = blockExecutor.Execute(block);

        Assert.Equal(2, executionInfoA.Executions.Length);
        Assert.Single(executionInfoA.EnterExecutions);
        Assert.Single(executionInfoA.LeaveExecutions);
        Assert.Equal(4, executionInfoA.Executions.Sum(i => i.Executions.Length));
        Assert.Equal(2, executionInfoA.Executions.Sum(i => i.EnterExecutions.Length));
        Assert.Equal(2, executionInfoA.Executions.Sum(i => i.LeaveExecutions.Length));

        var worldA0 = executionInfoA.Executions[0].Executions[0].LeaveWorld;
        Assert.Equal(
            ["foo", (BigInteger)1, (BigInteger)2, (BigInteger)1],
            signers.Select(signer => worldA0.GetSystemValue(signer.Address)));

        var worldA1 = executionInfoA.Executions[0].Executions[1].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", (BigInteger)2, (BigInteger)1],
            signers.Select(signer => worldA1.GetSystemValue(signer.Address)));

        var worldA2 = executionInfoA.Executions[1].Executions[0].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", "baz", (BigInteger)2],
            signers.Select(signer => worldA2.GetSystemValue(signer.Address)));

        var worldA3 = executionInfoA.Executions[1].Executions[1].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", "baz", "qux"],
            signers.Select(signer => worldA3.GetSystemValue(signer.Address)));

        Assert.IsType<InvalidCastException>(executionInfoA.Executions[1].LeaveExecutions[0].Exception);
        Assert.IsType<InvalidCastException>(executionInfoA.LeaveExecutions[0].Exception);

        var blockExecutedTask = blockchain.BlockExecuted.WaitAsync();
        blockchain.Append(block, blockCommit);
        var executionInfoB = await blockExecutedTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);

        Assert.Equal(2, executionInfoB.Executions.Length);
        Assert.Single(executionInfoB.EnterExecutions);
        Assert.Single(executionInfoB.LeaveExecutions);
        Assert.Equal(4, executionInfoB.Executions.Sum(i => i.Executions.Length));
        Assert.Equal(2, executionInfoB.Executions.Sum(i => i.EnterExecutions.Length));
        Assert.Equal(2, executionInfoB.Executions.Sum(i => i.LeaveExecutions.Length));

        var worldB0 = executionInfoB.Executions[0].Executions[0].LeaveWorld;
        Assert.Equal(
            ["foo", (BigInteger)1, (BigInteger)2, (BigInteger)1],
            signers.Select(signer => worldB0.GetSystemValue(signer.Address)));

        var worldB1 = executionInfoB.Executions[0].Executions[1].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", (BigInteger)2, (BigInteger)1],
            signers.Select(signer => worldB1.GetSystemValue(signer.Address)));

        var worldB2 = executionInfoB.Executions[1].Executions[0].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", "baz", (BigInteger)2],
            signers.Select(signer => worldB2.GetSystemValue(signer.Address)));

        var worldB3 = executionInfoB.Executions[1].Executions[1].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", "baz", "qux"],
            signers.Select(signer => worldB3.GetSystemValue(signer.Address)));

        Assert.IsType<InvalidCastException>(executionInfoB.Executions[1].LeaveExecutions[0].Exception);
        Assert.IsType<InvalidCastException>(executionInfoB.LeaveExecutions[0].Exception);

        Assert.Equal(executionInfoA.StateRootHash, executionInfoB.StateRootHash);
        Assert.True(repositoryA.States.Contains(executionInfoA.StateRootHash));
        Assert.True(repositoryB.States.Contains(executionInfoB.StateRootHash));
    }

    [Fact]
    public void EvaluateWithSystemActionsWithException()
    {
        var random = RandomUtility.GetRandom(output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 4);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EnterBlockActions =
                [
                    new UpdateValue { Address = signers[0].Address, Increment = 1 },
                    new ThrowException { ThrowOnExecution = true },
                ],
                LeaveBlockActions =
                [
                    new UpdateValue { Address = signers[1].Address, Increment = 1 },
                    new ThrowException { ThrowOnExecution = true },
                ],
                EnterTxActions =
                [
                    new UpdateValue { Address = signers[2].Address, Increment = 1 },
                    new ThrowException { ThrowOnExecution = true },
                ],
                LeaveTxActions =
                [
                    new UpdateValue { Address = signers[3].Address, Increment = 1 },
                    new ThrowException { ThrowOnExecution = true },
                ],
            },
        };
        var repository = new Repository();
        var blockchain = new Blockchain(genesisBlock, repository, options);

        blockchain.StagedTransactions.Add(proposer, @params: new()
        {
            Actions =
            [
                DumbAction.Create((signers[0].Address, "foo")),
                DumbAction.Create((signers[1].Address, "bar")),
            ],
        });
        blockchain.StagedTransactions.Add(proposer, @params: new()
        {
            Actions =
            [
                DumbAction.Create((signers[2].Address, "baz")),
                DumbAction.Create((signers[3].Address, "qux")),
            ],
        });

        var block = blockchain.Propose(proposer);
        var blockExecutor = new BlockExecutor(repository.States, options.SystemActions);
        var executionInfo = blockExecutor.Execute(block);

        Assert.Equal(2, executionInfo.Executions.Length);
        Assert.Equal(2, executionInfo.EnterExecutions.Length);
        Assert.Equal(2, executionInfo.LeaveExecutions.Length);
        Assert.Equal(4, executionInfo.Executions.Sum(i => i.Executions.Length));
        Assert.Equal(4, executionInfo.Executions.Sum(i => i.EnterExecutions.Length));
        Assert.Equal(4, executionInfo.Executions.Sum(i => i.LeaveExecutions.Length));

        Assert.IsType<ThrowException.SomeException>(executionInfo.EnterExecutions[1].Exception);
        Assert.IsType<ThrowException.SomeException>(executionInfo.LeaveExecutions[1].Exception);
        Assert.IsType<ThrowException.SomeException>(executionInfo.Executions[0].EnterExecutions[1].Exception);
        Assert.IsType<ThrowException.SomeException>(executionInfo.Executions[0].LeaveExecutions[1].Exception);
        Assert.IsType<ThrowException.SomeException>(executionInfo.Executions[1].EnterExecutions[1].Exception);
        Assert.IsType<ThrowException.SomeException>(executionInfo.Executions[1].LeaveExecutions[1].Exception);
    }

    [Fact]
    public void EvaluateWithException()
    {
        var random = RandomUtility.GetRandom(output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 4);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var repository = new Repository();
        var blockchain = new Blockchain(genesisBlock, repository);

        // var privateKey = new PrivateKey();
        // var address = privateKey.Address;

        var tx = new TransactionBuilder
        {
            Actions = [new ThrowException { ThrowOnExecution = true }],
        }.Create(proposer);

        _ = blockchain.StagedTransactions.Add(proposer, new()
        {
            Actions =
            [
                new ThrowException { ThrowOnExecution = true },
            ],
        });
        var block = blockchain.Propose(proposer);
        var blockExecutor = new BlockExecutor(repository.States);
        var executionInfo = blockExecutor.Execute(block);

        // var action = new ThrowException { ThrowOnExecution = true };

        // // var store = new Libplanet.Data.Store(new MemoryDatabase());
        // // var stateStore = new TrieStateStore();
        // var chain = TestUtils.MakeBlockChain(
        //     policy: new BlockPolicy(),
        //     store: store,
        //     stateStore: stateStore,
        //     actionLoader: new SingleActionLoader<ThrowException>());
        // var tx = new TransactionMetadata
        // {
        //         nonce: 0,
        //         privateKey: privateKey,
        //         genesisHash: blockchain.Genesis.Hash,
        //         actions: new[] { action }.ToPlainValues());

        // blockchain.StageTransaction(tx);

        // Block block = blockchain.ProposeBlock(new PrivateKey());
        // blockchain.Append(block, CreateBlockCommit(block));
        // var evaluations = blockchain.BlockExecutor.Evaluate(
        //     (RawBlock)blockchain.Tip, blockchain.Store.GetStateRootHash(blockchain.Tip.PreviousHash));

        // Assert.False(evaluations[0].InputContext.IsPolicyAction);
        // Assert.Single(evaluations);
        // Assert.NotNull(evaluations.Single().Exception);
        // Assert.IsType<UnexpectedlyTerminatedActionException>(
        //     evaluations.Single().Exception);
        // Assert.IsType<ThrowException.SomeException>(
        //     evaluations.Single().Exception.InnerException);
    }

    //     // [Fact]
    //     // public void EvaluateWithCriticalException()
    //     // {
    //     //     var privateKey = new PrivateKey();
    //     //     var address = privateKey.Address;

    //     //     var action = new ThrowException
    //     //     {
    //     //         ThrowOnExecution = true,
    //     //         Deterministic = false,
    //     //     };

    //     //     var store = new Libplanet.Data.Store(new MemoryDatabase());
    //     //     var stateStore =
    //     //         new TrieStateStore();
    //     //     var (chain, blockExecutor) =
    //     //         TestUtils.MakeBlockChainAndBlockExecutor(
    //     //             policy: new BlockPolicy(),
    //     //             store: store,
    //     //             stateStore: stateStore,
    //     //             actionLoader: new SingleActionLoader<ThrowException>());
    //     //     var genesis = blockchain.Genesis;
    //     //     // Evaluation is run with rehearsal true to get updated addresses on tx creation.
    //     //     var tx = new TransactionMetadata
    //     // {
    //     //         nonce: 0,
    //     //         privateKey: privateKey,
    //     //         genesisHash: genesis.Hash,
    //     //         actions: new[] { action }.ToPlainValues());
    //     //     var txs = new Transaction[] { tx };
    //     //     var evs = Array.Empty<EvidenceBase>();
    //     //     RawBlock block = RawBlock.Create(
    //     //         new BlockHeader
    //     //         {
    //     //             Height = 1L,
    //     //             Timestamp = DateTimeOffset.UtcNow,
    //     //             PublicKey = new PrivateKey().PublicKey,
    //     //             PreviousHash = genesis.Hash,
    //     //             TxHash = BlockContent.DeriveTxHash(txs),
    //     //
    //     //             EvidenceHash = null,
    //     //         },
    //     //         new BlockContent
    //     //         {
    //     //             Transactions = [.. txs],
    //     //             Evidence = [.. evs],
    //     //         });
    //     //     World previousState = stateStore.GetWorld(genesis.StateRootHash);

    //     //     Assert.Throws<OutOfMemoryException>(
    //     //         () => blockExecutor.EvaluateTx(
    //     //             block: block,
    //     //             tx: tx,
    //     //             world: previousState).ToList());
    //     //     Assert.Throws<OutOfMemoryException>(
    //     //         () => blockchain.BlockExecutor.Evaluate(
    //     //             block, blockchain.Store.GetStateRootHash(block.PreviousHash)).ToList());
    //     // }

    //     [Fact]
    //     public void EvaluateTxs()
    //     {
    //         static DumbAction MakeAction(Address address, char identifier, Address? transferTo = null)
    //         {
    //             return DumbAction.Create(
    //                 append: (address, identifier.ToString()),
    //                 transfer: transferTo is Address to ? (address, to, 5) : ((Address, Address, BigInteger)?)null);
    //         }

    //         Address[] addresses =
    //         [
    //             _txFx.Address1,
    //             _txFx.Address2,
    //             _txFx.Address3,
    //             _txFx.Address4,
    //             _txFx.Address5,
    //         ];

    //         var stateStore = new TrieStateStore();
    //         var world = World.Create(stateStore)
    //             .SetBalance(addresses[0], DumbAction.DumbCurrency * 100)
    //             .SetBalance(addresses[1], DumbAction.DumbCurrency * 100)
    //             .SetBalance(addresses[2], DumbAction.DumbCurrency * 100)
    //             .SetBalance(addresses[3], DumbAction.DumbCurrency * 100)
    //             .SetBalance(addresses[4], DumbAction.DumbCurrency * 100);
    //         var trie = stateStore.CommitWorld(world).Trie;

    //         var genesisBlock = ProposeGenesisBlock(TestUtils.GenesisProposer, stateRootHash: trie.Hash);
    //         var blockExecutor = new BlockExecutor(stateStore);

    //         var block1Txs = ImmutableSortedSet.Create(
    //         [
    //             new TransactionMetadata
    //             {
    //                 Signer = _txFx.PrivateKey1.Address,
    //                 GenesisHash = genesisBlock.BlockHash,
    //                 Timestamp = DateTimeOffset.MinValue.AddSeconds(2),
    //                 Actions = new IAction[]
    //                 {
    //                     MakeAction(addresses[0], 'A', addresses[1]),
    //                     MakeAction(addresses[1], 'B', addresses[2]),
    //                 }.ToBytecodes(),
    //             }.Sign(_txFx.PrivateKey1),
    //             new TransactionMetadata
    //             {
    //                 Signer = _txFx.PrivateKey2.Address,
    //                 GenesisHash = genesisBlock.BlockHash,
    //                 Timestamp = DateTimeOffset.MinValue.AddSeconds(4),
    //                 Actions = new IAction[]
    //                 {
    //                     MakeAction(addresses[2], 'C', addresses[3]),
    //                 }.ToBytecodes(),
    //             }.Sign(_txFx.PrivateKey2),
    //             new TransactionMetadata
    //             {
    //                 Signer = _txFx.PrivateKey3.Address,
    //                 GenesisHash = genesisBlock.BlockHash,
    //                 Timestamp = DateTimeOffset.MinValue.AddSeconds(7),
    //                 Actions = [],
    //             }.Sign(_txFx.PrivateKey3),
    //         ]);
    //         foreach ((var tx, var i) in block1Txs.Zip(
    //             Enumerable.Range(0, block1Txs.Count), (x, y) => (x, y)))
    //         {
    //             _logger.Debug("{0}[{1}] = {2}", nameof(block1Txs), i, tx.Id);
    //         }

    //         Block block1 = ProposeNextBlock(
    //             genesisBlock,
    //             GenesisProposer,
    //             block1Txs);
    //         World previousState = stateStore.GetWorld(genesisBlock.StateRootHash);
    //         var evals = blockExecutor.EvaluateBlock((RawBlock)block1, previousState);
    //         // Once the BlockHeader.CurrentProtocolVersion gets bumped, expectations may also
    //         // have to be updated, since the order may change due to different RawHash.
    //         (int TxIdx, int ActionIdx, string?[] UpdatedStates, Address Signer)[] expectations =
    //         [
    //             (0, 0, new[] { "A", null, null, null, null }, _txFx.Address1),  // Adds "A"
    //             (0, 1, new[] { "A", "B", null, null, null }, _txFx.Address1),   // Adds "B"
    //             (1, 0, new[] { "A", "B", "C", null, null }, _txFx.Address2),    // Adds "C"
    //         ];

    // #if DEBUG
    //         // This code was created by ilgyu.
    //         // You can preview the result of the test in the debug console.
    //         // If this test fails, you can copy the result from the debug console
    //         // and paste it to the upper part of the test code.
    //         System.Diagnostics.Trace.WriteLine("------- 1");
    //         foreach (var eval in evals)
    //         {
    //             int txIdx = block1Txs.Select((e, idx) => new { e, idx })
    //                 .First(x => x.e.Id.Equals(eval.InputContext.TxId))
    //                 .idx;
    //             int actionIdx = block1Txs[txIdx].Actions.Select((e, idx) => new { e, idx })
    //                 .First(x => x.e.Equals(eval.Action.ToBytecode()))
    //                 .idx;
    //             var outputWorld = eval.OutputWorld;
    //             var values = addresses.Select(item => outputWorld.GetValueOrDefault(SystemAccount, item));
    //             var valueStrings = values.Select(item => item is not null ? $"\"{item}\"" : "null");
    //             var updatedStates = "new[] { " + string.Join(", ", valueStrings) + " }";
    //             string signerIdx = "_txFx.Address" + (addresses.Select(
    //                 (e, idx) => new { e, idx }).First(
    //                 x => x.e.Equals(eval.InputContext.Signer)).idx + 1);
    //             System.Diagnostics.Trace.WriteLine(
    //                             $"({txIdx}, {actionIdx}, {updatedStates}, {signerIdx}),");
    //         }

    //         System.Diagnostics.Trace.WriteLine("---------");
    // #endif // DEBUG

    //         Assert.Equal(expectations.Length, evals.Length);
    //         foreach (var (expect, eval) in expectations.Zip(evals, (x, y) => (x, y)))
    //         {
    //             var outputWorld = eval.OutputWorld;
    //             Assert.Equal(
    //                 expect.UpdatedStates,
    //                 addresses.Select(item => outputWorld.GetValueOrDefault(SystemAccount, item)));
    //             Assert.Equal(block1Txs[expect.TxIdx].Id, eval.InputContext.TxId);
    //             Assert.Equal(
    //                 block1Txs[expect.TxIdx].Actions[expect.ActionIdx],
    //                 eval.Action.ToBytecode());
    //             Assert.Equal(expect.Signer, eval.InputContext.Signer);
    //             Assert.Equal(GenesisProposer.Address, eval.InputContext.Proposer);
    //             Assert.Equal(block1.Height, eval.InputContext.BlockHeight);
    //         }

    //         previousState = stateStore.GetWorld(genesisBlock.StateRootHash);
    //         ActionEvaluation[] evals1 =
    //             blockExecutor.EvaluateBlock((RawBlock)block1, previousState).ToArray();
    //         var output1 = World.Create(evals1[^1].OutputWorld.Trie, stateStore);
    //         Assert.Equal("A", output1.GetValue(SystemAccount, addresses[0]));
    //         Assert.Equal("B", output1.GetValue(SystemAccount, addresses[1]));
    //         Assert.Equal("C", output1.GetValue(SystemAccount, addresses[2]));
    //         Assert.Equal(
    //             FungibleAssetValue.Create(DumbAction.DumbCurrency, 95, 0),
    //             output1.GetBalance(addresses[0], DumbAction.DumbCurrency));
    //         Assert.Equal(
    //             FungibleAssetValue.Create(DumbAction.DumbCurrency, 100, 0),
    //             output1.GetBalance(addresses[1], DumbAction.DumbCurrency));
    //         Assert.Equal(
    //             FungibleAssetValue.Create(DumbAction.DumbCurrency, 100, 0),
    //             output1.GetBalance(addresses[2], DumbAction.DumbCurrency));
    //         Assert.Equal(
    //             FungibleAssetValue.Create(DumbAction.DumbCurrency, 105, 0),
    //             output1.GetBalance(addresses[3], DumbAction.DumbCurrency));

    //         var block2Txs = ImmutableSortedSet.Create(
    //         [
    //             // Note that these timestamps in themselves does not have any meanings but are
    //             // only arbitrary.  These purpose to make their evaluation order in a block
    //             // equal to the order we (the test) intend:
    //             new TransactionMetadata
    //             {
    //                 Signer = _txFx.PrivateKey1.Address,
    //                 GenesisHash = genesisBlock.BlockHash,
    //                 Timestamp = DateTimeOffset.MinValue.AddSeconds(3),
    //                 Actions = new IAction[]
    //                 {
    //                     MakeAction(addresses[0], 'D'),
    //                 }.ToBytecodes(),
    //             }.Sign(_txFx.PrivateKey1),
    //             new TransactionMetadata
    //             {
    //                 Signer = _txFx.PrivateKey2.Address,
    //                 GenesisHash = genesisBlock.BlockHash,
    //                 Timestamp = DateTimeOffset.MinValue.AddSeconds(2),
    //                 Actions = new[]
    //                 {
    //                     MakeAction(addresses[3], 'E'),
    //                 }.ToBytecodes(),
    //             }.Sign(_txFx.PrivateKey2),
    //             new TransactionMetadata
    //             {
    //                 Signer = _txFx.PrivateKey3.Address,
    //                 GenesisHash = genesisBlock.BlockHash,
    //                 Timestamp = DateTimeOffset.MinValue.AddSeconds(5),
    //                 Actions = new[]
    //                 {
    //                     DumbAction.Create((addresses[4], "F"), transfer: (addresses[0], addresses[4], 8)),
    //                 }.ToBytecodes(),
    //             }.Sign(_txFx.PrivateKey3),
    //         ]);
    //         foreach ((var tx, var i) in block2Txs.Zip(
    //             Enumerable.Range(0, block2Txs.Count), (x, y) => (x, y)))
    //         {
    //             _logger.Debug("{0}[{1}] = {2}", nameof(block2Txs), i, tx.Id);
    //         }

    //         // Same as above, use the same timestamp of last commit for each to get a deterministic
    //         // test result.
    //         Block block2 = ProposeNextBlock(
    //             block1,
    //             GenesisProposer,
    //             block2Txs,
    //             lastCommit: CreateBlockCommit(block1, true));

    //         // Forcefully reset to null delta
    //         previousState = evals1[^1].OutputWorld;
    //         evals = blockExecutor.EvaluateBlock((RawBlock)block2, previousState);

    //         // Once the BlockHeader.CurrentProtocolVersion gets bumped, expectations may also
    //         // have to be updated, since the order may change due to different RawHash.
    //         expectations =
    //         [
    //             (0, 0, new[] { "A", "B", "C", "E", null }, _txFx.Address2),
    //             (1, 0, new[] { "A,D", "B", "C", "E", null }, _txFx.Address1),
    //             (2, 0, new[] { "A,D", "B", "C", "E", "F" }, _txFx.Address3),
    //         ];

    // #if DEBUG
    //         // This code was created by ilgyu.
    //         // You can preview the result of the test in the debug console.
    //         // If this test fails, you can copy the result from the debug console
    //         // and paste it to the upper part of the test code.
    //         System.Diagnostics.Trace.WriteLine("------- 2");
    //         foreach (var eval in evals)
    //         {
    //             int txIdx = block2Txs.Select(
    //                 (e, idx) => new { e, idx }).First(
    //                 x => x.e.Id.Equals(eval.InputContext.TxId)).idx;
    //             int actionIdx = block2Txs[txIdx].Actions.Select(
    //                 (e, idx) => new { e, idx }).First(
    //                 x => x.e.Equals(eval.Action.ToBytecode())).idx;
    //             string updatedStates = "new[] { " + string.Join(", ", addresses.Select(
    //                 eval.OutputWorld.GetAccount(SystemAccount).GetValueOrDefault)
    //                 .Select(x => x is string t ? '"' + t + '"' : "null")) + " }";
    //             string signerIdx = "_txFx.Address" + (addresses.Select(
    //                 (e, idx) => new { e, idx }).First(
    //                 x => x.e.Equals(eval.InputContext.Signer)).idx + 1);
    //             System.Diagnostics.Trace.WriteLine(
    //                 $"({txIdx}, {actionIdx}, {updatedStates}, {signerIdx}),");
    //         }

    //         System.Diagnostics.Trace.WriteLine("---------");
    // #endif // DEBUG

    //         Assert.Equal(expectations.Length, evals.Length);
    //         foreach (var (expect, eval) in expectations.Zip(evals, (x, y) => (x, y)))
    //         {
    //             var updatedStates = addresses
    //                 .Select(eval.OutputWorld.GetAccount(SystemAccount).GetValueOrDefault);
    //             Assert.Equal(expect.UpdatedStates, updatedStates);
    //             Assert.Equal(block2Txs[expect.TxIdx].Id, eval.InputContext.TxId);
    //             Assert.Equal(
    //                 block2Txs[expect.TxIdx].Actions[expect.ActionIdx],
    //                 eval.Action.ToBytecode());
    //             Assert.Equal(expect.Signer, eval.InputContext.Signer);
    //             Assert.Equal(GenesisProposer.Address, eval.InputContext.Proposer);
    //             Assert.Equal(block2.Height, eval.InputContext.BlockHeight);
    //             Assert.Null(eval.Exception);
    //         }

    //         previousState = evals1[^1].OutputWorld;
    //         var evals2 = blockExecutor.EvaluateBlock((RawBlock)block2, previousState).ToArray();
    //         var output2 = World.Create(evals2[^1].OutputWorld.Trie, stateStore);
    //         Assert.Equal("A,D", output2.GetValue(SystemAccount, addresses[0]));
    //         Assert.Equal("E", output2.GetValue(SystemAccount, addresses[3]));
    //         Assert.Equal("F", output2.GetValue(SystemAccount, addresses[4]));
    //     }

    //     [Fact]
    //     public void EvaluateTx()
    //     {
    //         PrivateKey[] keys = [new PrivateKey(), new PrivateKey(), new PrivateKey()];
    //         Address[] addresses = keys.Select(key => key.Address).ToArray();
    //         DumbAction[] actions =
    //         [
    //             DumbAction.Create(
    //                 append: (addresses[0], "0"),
    //                 transfer: (addresses[0], addresses[1], 5)),
    //             DumbAction.Create(
    //                 append: (addresses[1], "1"),
    //                 transfer: (addresses[2], addresses[1], 10)),
    //             DumbAction.Create(
    //                 append: (addresses[0], "2"),
    //                 transfer: (addresses[1], addresses[0], 10)),
    //             DumbAction.Create((addresses[2], "R")),
    //         ];
    //         var tx = new TransactionMetadata
    //         {
    //             Nonce = 0,
    //             Signer = _txFx.PrivateKey1.Address,
    //             GenesisHash = default,
    //             Actions = actions.ToBytecodes(),
    //         }.Sign(_txFx.PrivateKey1);
    //         var txs = new Transaction[] { tx };
    //         var evs = Array.Empty<EvidenceBase>();
    //         var block = new RawBlock
    //         {
    //             Header = new BlockHeader
    //             {
    //                 Height = 1,
    //                 Timestamp = DateTimeOffset.UtcNow,
    //                 Proposer = keys[0].Address,
    //             },
    //             Content = new BlockContent
    //             {
    //                 Transactions = [.. txs],
    //                 Evidences = [.. evs],
    //             },
    //         };
    //         TrieStateStore stateStore = new TrieStateStore();
    //         World world = World.Create(stateStore)
    //             .SetBalance(addresses[0], DumbAction.DumbCurrency * 100)
    //             .SetBalance(addresses[1], DumbAction.DumbCurrency * 100)
    //             .SetBalance(addresses[2], DumbAction.DumbCurrency * 100);
    //         var initTrie = stateStore.CommitWorld(world).Trie;
    //         var blockExecutor = new BlockExecutor(stateStore);
    //         var previousState = stateStore.GetWorld(initTrie.Hash);
    //         var evaluations = blockExecutor.EvaluateTx(
    //             rawBlock: block,
    //             tx: tx,
    //             world: previousState).ToImmutableArray();

    //         Assert.Equal(actions.Length, evaluations.Length);
    //         string?[][] expectedStates =
    //         [
    //             ["0", null, null],
    //             ["0", "1", null],
    //             ["0,2", "1", null],
    //             ["0,2", "1", "R"],
    //         ];
    //         BigInteger[][] expectedBalances =
    //         [
    //             [95, 105, 100],
    //             [95, 115, 90],
    //             [105, 105, 90],
    //             [105, 105, 90],
    //         ];

    //         Currency currency = DumbAction.DumbCurrency;
    //         BigInteger[] initBalances = [100, 100, 100];
    //         for (int i = 0; i < evaluations.Length; i++)
    //         {
    //             var evaluation = evaluations[i];
    //             var inputContext = evaluation.InputContext;
    //             _logger.Debug("evalsA[{0}] = {1}", i, evaluation);
    //             _logger.Debug("txA.Actions[{0}] = {1}", i, tx.Actions[i]);

    //             Assert.Equal(tx.Actions[i], evaluation.Action.ToBytecode());
    //             Assert.Equal(_txFx.Address1, inputContext.Signer);
    //             Assert.Equal(tx.Id, inputContext.TxId);
    //             Assert.Equal(addresses[0], inputContext.Proposer);
    //             Assert.Equal(1, inputContext.BlockHeight);
    //             var prevEval = i > 0 ? evaluations[i - 1] : null;
    //             Assert.Equal(
    //                 addresses.Select(item => prevEval?.OutputWorld?.GetValueOrDefault(SystemAccount, item)),
    //                 addresses.Select(item => evaluation.InputWorld.GetValueOrDefault(SystemAccount, item)));
    //             Assert.Equal(
    //                 expectedStates[i],
    //                 addresses.Select(item => (string?)evaluation.OutputWorld.GetValueOrDefault(SystemAccount, item)));
    //             Assert.Equal(
    //                 prevEval is null
    //                     ? initBalances
    //                     : addresses.Select(a =>
    //                         prevEval.OutputWorld
    //                             .GetBalance(a, currency).RawValue),
    //                 addresses.Select(
    //                     a => evaluation.InputWorld
    //                             .GetBalance(a, currency).RawValue));
    //             Assert.Equal(
    //                 expectedBalances[i],
    //                 addresses.Select(a => evaluation.OutputWorld
    //                     .GetBalance(a, currency).RawValue));
    //         }

    //         previousState = stateStore.GetWorld(initTrie.Hash);
    //         World delta = blockExecutor.EvaluateTx(
    //             rawBlock: block,
    //             tx: tx,
    //             world: previousState)[^1].OutputWorld;
    //         Assert.Empty(evaluations[3].OutputWorld.Trie.Diff(delta.Trie));
    //     }

    //     [Fact]
    //     public void EvaluateTxResultThrowingException()
    //     {
    //         var action = new ThrowException { ThrowOnExecution = true };
    //         var tx = new TransactionMetadata
    //         {
    //             Signer = _txFx.PrivateKey1.Address,
    //             Actions = new[] { action }.ToBytecodes(),
    //         }.Sign(_txFx.PrivateKey1);
    //         var txs = ImmutableSortedSet.Create(tx);
    //         var hash = new BlockHash(GetRandomBytes(BlockHash.Size));
    //         var stateStore = new TrieStateStore();
    //         var blockExecutor = new BlockExecutor(stateStore);
    //         var block = new RawBlock
    //         {
    //             Header = new BlockHeader
    //             {
    //                 Height = 123,
    //                 Timestamp = DateTimeOffset.UtcNow,
    //                 Proposer = GenesisProposer.Address,
    //                 PreviousHash = hash,
    //                 LastCommit = CreateBlockCommit(hash, 122, 0),
    //             },
    //             Content = new BlockContent
    //             {
    //                 Transactions = txs,
    //                 Evidences = [],
    //             },
    //         };
    //         var world = stateStore.GetWorld(default);
    //         var nextWorld = blockExecutor.EvaluateTx(block, tx, world)[^1].OutputWorld;

    //         Assert.Empty(nextWorld.Trie.Diff(world.Trie));
    //     }

    //     [Fact]
    //     public void EvaluateActions()
    //     {
    //         var fx = new IntegerSet([5, 10]);
    //         var blockExecutor = new BlockExecutor(fx.StateStore);

    //         // txA: ((5 + 1) * 2) + 3 = 15
    //         (Transaction txA, var deltaA) = fx.Sign(
    //             0,
    //             Arithmetic.Add(1),
    //             Arithmetic.Mul(2),
    //             Arithmetic.Add(3));

    //         Block blockA = fx.Propose();
    //         fx.Append(blockA);
    //         ActionEvaluation[] evalsA = blockExecutor.EvaluateActions(
    //             rawBlock: (RawBlock)blockA,
    //             tx: txA,
    //             world: fx.StateStore.GetWorld(blockA.StateRootHash),
    //             actions: [.. txA.Actions.Select(item => item.ToAction<IAction>())]);

    //         Assert.Equal(evalsA.Length, deltaA.Count - 1);
    //         Assert.Equal(
    //             (BigInteger)15,
    //             evalsA[^1].OutputWorld.GetValue(SystemAccount, txA.Signer));
    //         // Assert.All(evalsA, eval => Assert.Empty(eval.InputContext.Txs));

    //         for (int i = 0; i < evalsA.Length; i++)
    //         {
    //             ActionEvaluation eval = evalsA[i];
    //             IActionContext context = eval.InputContext;
    //             World prevState = eval.InputWorld;
    //             World outputState = eval.OutputWorld;
    //             _logger.Debug("evalsA[{0}] = {1}", i, eval);
    //             _logger.Debug("txA.Actions[{0}] = {1}", i, txA.Actions[i]);

    //             Assert.Equal(txA.Actions[i], eval.Action.ToBytecode());
    //             Assert.Equal(txA.Id, context.TxId);
    //             Assert.Equal(blockA.Proposer, context.Proposer);
    //             Assert.Equal(blockA.Height, context.BlockHeight);
    //             Assert.Equal(txA.Signer, context.Signer);
    //             Assert.Equal(
    //                 deltaA[i].Value,
    //                 prevState.GetValue(SystemAccount, txA.Signer));
    //             Assert.Equal(
    //                 ToStateKey(txA.Signer),
    //                 Assert.Single(
    //                     outputState.GetAccount(SystemAccount).Trie.Diff(
    //                         prevState.GetAccount(SystemAccount).Trie))
    //                 .Path);
    //             Assert.Equal(
    //                 deltaA[i + 1].Value,
    //                 outputState.GetValue(SystemAccount, txA.Signer));
    //             Assert.Null(eval.Exception);
    //         }

    //         // txB: error(10 - 3) + -1 =
    //         //           (10 - 3) + -1 = 6  (error() does nothing)
    //         (Transaction txB, var deltaB) = fx.Sign(
    //             1,
    //             Arithmetic.Sub(3),
    //             new Arithmetic { Error = "error" },
    //             Arithmetic.Add(-1));

    //         Block blockB = fx.Propose();
    //         fx.Append(blockB);
    //         ActionEvaluation[] evalsB = blockExecutor.EvaluateActions(
    //             rawBlock: (RawBlock)blockB,
    //             tx: txB,
    //             world: fx.StateStore.GetWorld(blockB.StateRootHash),
    //             actions: [.. txB.Actions.Select(item => item.ToAction<IAction>())]);

    //         Assert.Equal(evalsB.Length, deltaB.Count - 1);
    //         Assert.Equal(
    //             (BigInteger)6,
    //             evalsB[^1].OutputWorld.GetValue(SystemAccount, txB.Signer));

    //         for (int i = 0; i < evalsB.Length; i++)
    //         {
    //             ActionEvaluation eval = evalsB[i];
    //             IActionContext context = eval.InputContext;
    //             World prevState = eval.InputWorld;
    //             World outputState = eval.OutputWorld;

    //             _logger.Debug("evalsB[{0}] = {@1}", i, eval);
    //             _logger.Debug("txB.Actions[{0}] = {@1}", i, txB.Actions[i]);

    //             Assert.Equal(txB.Actions[i], eval.Action.ToBytecode());
    //             Assert.Equal(txB.Id, context.TxId);
    //             Assert.Equal(blockB.Proposer, context.Proposer);
    //             Assert.Equal(blockB.Height, context.BlockHeight);
    //             Assert.Equal(txB.Signer, context.Signer);
    //             Assert.Equal(
    //                 deltaB[i].Value,
    //                 prevState.GetValue(SystemAccount, txB.Signer));
    //             Assert.Equal(
    //                 deltaB[i + 1].Value,
    //                 outputState.GetValue(SystemAccount, txB.Signer));
    //             if (i == 1)
    //             {
    //                 Assert.Empty(outputState.Trie.Diff(prevState.Trie));
    //                 Assert.IsType<InvalidOperationException>(eval.Exception);
    //                 Assert.Equal("error", eval.Exception.Message);
    //             }
    //             else
    //             {
    //                 Assert.Equal(
    //                     ToStateKey(txB.Signer),
    //                     Assert.Single(outputState.GetAccount(SystemAccount).Trie.Diff(prevState.GetAccount(SystemAccount).Trie)).Path);
    //                 Assert.Null(eval.Exception);
    //             }
    //         }
    //     }

    //     [Fact]
    //     public void EvaluatePolicyEnterBlockActions()
    //     {
    //         var (chain, blockExecutor) = MakeBlockChainAndBlockExecutor(
    //             options: _options,
    //             genesisBlock: _storeFx.GenesisBlock,
    //             privateKey: GenesisProposer);
    //         (_, Transaction[] txs) = MakeFixturesForAppendTests();
    //         var genesis = blockchain.Genesis;
    //         var block = blockchain.ProposeBlock(proposer: GenesisProposer);

    //         World previousState = blockchain.GetWorld(stateRootHash: default);
    //         var evaluations = blockExecutor.EvaluateEnterBlockActions(
    //             (RawBlock)genesis,
    //             previousState);

    //         Assert.Equal<IAction>(
    //             blockchain.Options.SystemActions.EnterBlockActions,
    //             ImmutableArray.ToImmutableArray(evaluations.Select(item => item.Action)));
    //         Assert.Single(evaluations);
    //         Assert.Equal(
    //             (BigInteger)1,
    //             (BigInteger)evaluations[0].OutputWorld.GetValue(SystemAccount, _beginBlockValueAddress));

    //         previousState = evaluations[0].OutputWorld;
    //         evaluations = blockExecutor.EvaluateEnterBlockActions(
    //             (RawBlock)block,
    //             previousState);

    //         Assert.Equal<IAction>(
    //             blockchain.Options.SystemActions.EnterBlockActions,
    //             ImmutableArray.ToImmutableArray(evaluations.Select(item => item.Action)));
    //         Assert.Single(evaluations);
    //         Assert.Equal(
    //             (BigInteger)2,
    //             (BigInteger)evaluations[0].OutputWorld.GetValue(SystemAccount, _beginBlockValueAddress));
    //     }

    //     [Fact]
    //     public void EvaluatePolicyLeaveBlockActions()
    //     {
    //         var (chain, blockExecutor) = MakeBlockChainAndBlockExecutor(
    //             options: _options,
    //             genesisBlock: _storeFx.GenesisBlock,
    //             privateKey: GenesisProposer);
    //         (_, Transaction[] txs) = MakeFixturesForAppendTests();
    //         var genesis = blockchain.Genesis;
    //         var block = blockchain.ProposeBlock(GenesisProposer);

    //         World previousState = blockchain.GetWorld(stateRootHash: default);
    //         var evaluations = blockExecutor.EvaluateLeaveBlockActions(
    //             (RawBlock)genesis,
    //             previousState);

    //         Assert.Equal<IAction>(
    //             blockchain.Options.SystemActions.LeaveBlockActions,
    //             ImmutableArray.ToImmutableArray(evaluations.Select(item => item.Action)));
    //         Assert.Single(evaluations);
    //         Assert.Equal(
    //             (BigInteger)1,
    //             evaluations[0].OutputWorld.GetValue(SystemAccount, _endBlockValueAddress));

    //         previousState = evaluations[0].OutputWorld;
    //         evaluations = blockExecutor.EvaluateLeaveBlockActions(
    //             (RawBlock)block,
    //             previousState);

    //         Assert.Equal<IAction>(
    //             blockchain.Options.SystemActions.LeaveBlockActions,
    //             ImmutableArray.ToImmutableArray(evaluations.Select(item => item.Action)));
    //         Assert.Single(evaluations);
    //         Assert.Equal(
    //             (BigInteger)2,
    //             evaluations[0].OutputWorld.GetValue(SystemAccount, _endBlockValueAddress));
    //     }

    //     [Fact]
    //     public void EvaluatePolicyEnterTxActions()
    //     {
    //         var (chain, blockExecutor) = MakeBlockChainAndBlockExecutor(
    //             options: _options,
    //             genesisBlock: _storeFx.GenesisBlock,
    //             privateKey: GenesisProposer);
    //         (_, Transaction[] txs) = MakeFixturesForAppendTests();
    //         var genesis = blockchain.Genesis;
    //         var block = blockchain.ProposeBlock(proposer: GenesisProposer);

    //         World previousState = blockchain.GetWorld(stateRootHash: default);
    //         var evaluations = blockExecutor.EvaluateEnterTxActions(
    //             (RawBlock)genesis,
    //             txs[0],
    //             previousState);

    //         Assert.Equal<IAction>(
    //             blockchain.Options.SystemActions.EnterTxActions,
    //             ImmutableArray.ToImmutableArray(evaluations.Select(item => item.Action)));
    //         Assert.Single(evaluations);
    //         Assert.Equal(
    //             (BigInteger)1,
    //             evaluations[0].OutputWorld.GetValue(SystemAccount, _beginTxValueAddress));
    //         Assert.Equal(txs[0].Signer, evaluations[0].InputContext.Signer);

    //         previousState = evaluations[0].OutputWorld;
    //         evaluations = blockExecutor.EvaluateEnterTxActions(
    //             (RawBlock)block,
    //             txs[1],
    //             previousState);

    //         Assert.Equal<IAction>(
    //             blockchain.Options.SystemActions.EnterTxActions,
    //             ImmutableArray.ToImmutableArray(evaluations.Select(item => item.Action)));
    //         Assert.Single(evaluations);
    //         Assert.Equal(
    //             (BigInteger)2,
    //             evaluations[0].OutputWorld.GetValue(SystemAccount, _beginTxValueAddress));
    //         Assert.Equal(txs[1].Signer, evaluations[0].InputContext.Signer);
    //     }

    //     [Fact]
    //     public void EvaluatePolicyLeaveTxActions()
    //     {
    //         var (chain, blockExecutor) = MakeBlockChainAndBlockExecutor(
    //             options: _options,
    //             genesisBlock: _storeFx.GenesisBlock,
    //             privateKey: GenesisProposer);
    //         (_, Transaction[] txs) = MakeFixturesForAppendTests();
    //         var genesis = blockchain.Genesis;
    //         var block = blockchain.ProposeBlock(proposer: GenesisProposer);

    //         World previousState = blockchain.GetWorld(stateRootHash: default);
    //         var evaluations = blockExecutor.EvaluateLeaveTxActions(
    //             (RawBlock)genesis,
    //             txs[0],
    //             previousState);

    //         Assert.Equal<IAction>(
    //             blockchain.Options.SystemActions.LeaveTxActions,
    //             ImmutableArray.ToImmutableArray(evaluations.Select(item => item.Action)));
    //         Assert.Single(evaluations);
    //         Assert.Equal(
    //             (BigInteger)1,
    //             evaluations[0].OutputWorld.GetValue(SystemAccount, _endTxValueAddress));
    //         Assert.Equal(txs[0].Signer, evaluations[0].InputContext.Signer);

    //         previousState = evaluations[0].OutputWorld;
    //         evaluations = blockExecutor.EvaluateLeaveTxActions(
    //             (RawBlock)block,
    //             txs[1],
    //             previousState);

    //         Assert.Equal<IAction>(
    //             blockchain.Options.SystemActions.LeaveTxActions,
    //             ImmutableArray.ToImmutableArray(evaluations.Select(item => item.Action)));
    //         Assert.Single(evaluations);
    //         Assert.Equal(
    //             (BigInteger)2,
    //             evaluations[0].OutputWorld.GetValue(SystemAccount, _endTxValueAddress));
    //         Assert.Equal(txs[1].Signer, evaluations[0].InputContext.Signer);
    //     }

    //     [Fact]
    //     public void EvaluateActionAndCollectFee()
    //     {
    //         var privateKey = new PrivateKey();
    //         var address = privateKey.Address;
    //         Currency foo = Currency.Create("FOO", 18);

    //         var freeGasAction = new UseGasAction()
    //         {
    //             GasUsage = 0,
    //             Memo = "FREE",
    //             MintValue = FungibleAssetValue.Create(foo, 10),
    //             Receiver = address,
    //         };

    //         var payGasAction = new UseGasAction()
    //         {
    //             GasUsage = 1,
    //             Memo = "CHARGE",
    //         };

    //         var repository = new Repository();
    //         var options = new BlockchainOptions();
    //         var chain = TestUtils.MakeBlockChain(
    //             options: options,
    //             actions: [freeGasAction]);
    //         var blockExecutor = new BlockExecutor(repository.StateStore, options.SystemActions);
    //         var tx = new TransactionMetadata
    //         {
    //             Signer = privateKey.Address,
    //             GenesisHash = blockchain.Genesis.BlockHash,
    //             MaxGasPrice = FungibleAssetValue.Create(foo, 1),
    //             GasLimit = 3,
    //             Actions = new[]
    //             {
    //                 payGasAction,
    //             }.ToBytecodes(),
    //         }.Sign(privateKey);

    //         blockchain.StagedTransactions.Add(tx);
    //         var miner = new PrivateKey();
    //         Block block = blockchain.ProposeBlock(miner);

    //         var evaluations = blockExecutor.Evaluate(
    //             (RawBlock)block, blockchain.GetNextStateRootHash(block.PreviousHash) ?? default);

    //         Assert.Single(evaluations);
    //         Assert.Null(evaluations.Single().Exception);
    //         Assert.Equal(2, GasTracer.GasAvailable);
    //         Assert.Equal(1, GasTracer.GasUsed);
    //     }

    //     [Fact]
    //     public void EvaluateThrowingExceedGasLimit()
    //     {
    //         var privateKey = new PrivateKey();
    //         var address = privateKey.Address;
    //         Currency foo = Currency.Create("FOO", 18);

    //         var freeGasAction = new UseGasAction()
    //         {
    //             GasUsage = 0,
    //             Memo = "FREE",
    //             MintValue = FungibleAssetValue.Create(foo, 10),
    //             Receiver = address,
    //         };

    //         var payGasAction = new UseGasAction()
    //         {
    //             GasUsage = 10,
    //             Memo = "CHARGE",
    //         };

    //         var repository = new Repository();
    //         var options = new BlockchainOptions();
    //         var chain = TestUtils.MakeBlockChain(
    //             options: options,
    //             actions: new[]
    //             {
    //                 freeGasAction,
    //             });
    //         var blockExecutor = new BlockExecutor(repository.StateStore, options.SystemActions);
    //         var tx = new TransactionMetadata
    //         {
    //             Signer = privateKey.Address,
    //             GenesisHash = blockchain.Genesis.BlockHash,
    //             Actions = new[] { payGasAction }.ToBytecodes(),
    //             MaxGasPrice = FungibleAssetValue.Create(foo, 1),
    //             GasLimit = 5,
    //         }.Sign(privateKey);

    //         blockchain.StagedTransactions.Add(tx);
    //         var miner = new PrivateKey();
    //         Block block = blockchain.ProposeBlock(miner);

    //         var evaluations = blockExecutor.Evaluate(
    //             (RawBlock)block,
    //             blockchain.GetNextStateRootHash(block.PreviousHash) ?? default);

    //         Assert.Single(evaluations);
    //         Assert.IsType<InvalidOperationException>(evaluations.Single().Exception);
    //         Assert.Equal(0, GasTracer.GasAvailable);
    //         Assert.Equal(5, GasTracer.GasUsed);
    //     }

    //     [Fact]
    //     public void GenerateRandomSeed()
    //     {
    //         byte[] preEvaluationHashBytes =
    //         [
    //             0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
    //             0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
    //         ];
    //         var signature = ImmutableArray.Create<byte>(
    //         [
    //             0x30, 0x44, 0x02, 0x20, 0x2f, 0x2d, 0xbe, 0x5a, 0x91, 0x65, 0x59, 0xde, 0xdb,
    //             0xe8, 0xd8, 0x4f, 0xa9, 0x20, 0xe2, 0x01, 0x29, 0x4d, 0x4f, 0x40, 0xea, 0x1e,
    //             0x97, 0x44, 0x1f, 0xbf, 0xa2, 0x5c, 0x8b, 0xd0, 0x0e, 0x23, 0x02, 0x20, 0x3c,
    //             0x06, 0x02, 0x1f, 0xb8, 0x3f, 0x67, 0x49, 0x92, 0x3c, 0x07, 0x59, 0x67, 0x96,
    //             0xa8, 0x63, 0x04, 0xb0, 0xc3, 0xfe, 0xbb, 0x6c, 0x7a, 0x7b, 0x58, 0x58, 0xe9,
    //             0x7d, 0x37, 0x67, 0xe1, 0xe9,
    //         ]);

    //         int seed = BlockExecutor.GenerateRandomSeed(preEvaluationHashBytes, signature);
    //         Assert.Equal(353767086, seed);
    //     }

    //     [Fact]
    //     public void CheckRandomSeedInAction()
    //     {
    //         var fx = new IntegerSet([5, 10]);
    //         var blockExecutor = new BlockExecutor(fx.StateStore);

    //         // txA: ((5 + 1) * 2) + 3 = 15
    //         (Transaction tx, var delta) = fx.Sign(
    //             0,
    //             Arithmetic.Add(1),
    //             Arithmetic.Mul(2),
    //             Arithmetic.Add(3));

    //         var block = fx.Propose();
    //         var rawBlock = (RawBlock)block;
    //         var evaluations = blockExecutor.EvaluateActions(
    //             rawBlock: rawBlock,
    //             tx: tx,
    //             world: fx.StateStore.GetWorld(block.StateRootHash),
    //             actions: [.. tx.Actions.Select(item => item.ToAction<IAction>())]);

    //         byte[] preEvaluationHashBytes = rawBlock.Hash.Bytes.ToArray();
    //         var randomSeeds = Enumerable
    //             .Range(0, tx.Actions.Length)
    //             .Select(offset => BlockExecutor.GenerateRandomSeed(preEvaluationHashBytes, tx.Signature) + offset)
    //             .ToArray();

    //         for (var i = 0; i < evaluations.Length; i++)
    //         {
    //             var evaluation = evaluations[i];
    //             var context = evaluation.InputContext;
    //             Assert.Equal(randomSeeds[i], context.RandomSeed);
    //         }
    //     }

    // private (Address[], Transaction[]) MakeFixturesForAppendTests(
    //     PrivateKey? privateKey = null, DateTimeOffset? epoch = null)
    // {
    //     Address[] addresses =
    //     [
    //         _storeFx.Address1,
    //             _storeFx.Address2,
    //             _storeFx.Address3,
    //             _storeFx.Address4,
    //             _storeFx.Address5,
    //         ];

    //     privateKey ??= new PrivateKey(
    //     [
    //         0xa8, 0x21, 0xc7, 0xc2, 0x08, 0xa9, 0x1e, 0x53, 0xbb, 0xb2,
    //             0x71, 0x15, 0xf4, 0x23, 0x5d, 0x82, 0x33, 0x44, 0xd1, 0x16,
    //             0x82, 0x04, 0x13, 0xb6, 0x30, 0xe7, 0x96, 0x4f, 0x22, 0xe0,
    //             0xec, 0xe0,
    //         ]);
    //     epoch ??= DateTimeOffset.UtcNow;

    //     Transaction[] txs =
    //     [
    //         _storeFx.MakeTransaction(
    //                 [
    //                     DumbAction.Create((addresses[0], "foo")),
    //                     DumbAction.Create((addresses[1], "bar")),
    //                 ],
    //                 timestamp: epoch,
    //                 nonce: 0,
    //                 privateKey: privateKey),
    //             _storeFx.MakeTransaction(
    //                 [
    //                     DumbAction.Create((addresses[2], "baz")),
    //                     DumbAction.Create((addresses[3], "qux")),
    //                 ],
    //                 timestamp: epoch.Value.AddSeconds(5),
    //                 nonce: 1,
    //                 privateKey: privateKey),
    //         ];

    //     return (addresses, txs);
    // }

    [Model(Version = 1, TypeName = "UseGasAction")]
    private sealed record class UseGasAction : ActionBase
    {
        [Property(0)]
        public long GasUsage { get; set; }

        [Property(1)]
        public string Memo { get; set; } = string.Empty;

        [Property(2)]
        public FungibleAssetValue? MintValue { get; set; }

        [Property(3)]
        public Address? Receiver { get; set; }

        protected override void OnExecute(IWorldContext world, IActionContext context)
        {
            GasTracer.UseGas(GasUsage);
            world[SystemAccount, context.Signer] = Memo;

            if (Receiver is { } receiver && MintValue is { } mintValue)
            {
                world.MintAsset(receiver, mintValue);
            }
        }
    }
}
