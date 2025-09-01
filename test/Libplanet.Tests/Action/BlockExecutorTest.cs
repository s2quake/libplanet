using System.ComponentModel;
using Libplanet.Data;
using Libplanet.Extensions;
using Libplanet.Serialization;
using Libplanet.State;
using Libplanet.State.Builtin;
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
        var txAddress = signer.Address;
        var txs = new[]
        {
            new TransactionBuilder
            {
                Actions = [new ContextRecording { Address = txAddress, Value = "Foo" }],
            }.Create(signer),
        };
        var stateIndex = new StateIndex();
        var blockExecutor = new BlockExecutor(stateIndex);
        var block = new BlockBuilder
        {
            Transactions = [.. txs],
        }.Create(proposer);

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

        var execution = blockExecutor.Execute(blockchain.Tip);
        var txExecution = Assert.Single(execution.Executions);
        var actionExecution = Assert.Single(txExecution.Executions);
        var world = new World(execution.LeaveWorld.Trie, repositoryA.States);
        Assert.Equal(blockchain.StateRootHash, execution.StateRootHash);
        Assert.Null(actionExecution.Exception);
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
            actionExecution.ActionContext.GetRandom().Next(),
            blockchain.GetSystemValue(ContextRecording.RandomRecordAddress));
        Assert.Equal(
            actionExecution.ActionContext.GetRandom().Next(),
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
            SystemAction = new SystemAction
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

        var blockExecutor = new BlockExecutor(repositoryA.States);
        var executionA = blockExecutor.Execute(block, options.SystemAction);

        Assert.Equal(2, executionA.Executions.Length);
        Assert.Single(executionA.EnterExecutions);
        Assert.Single(executionA.LeaveExecutions);
        Assert.Equal(4, executionA.Executions.Sum(i => i.Executions.Length));
        Assert.Equal(2, executionA.Executions.Sum(i => i.EnterExecutions.Length));
        Assert.Equal(2, executionA.Executions.Sum(i => i.LeaveExecutions.Length));

        var worldA0 = executionA.Executions[0].Executions[0].LeaveWorld;
        Assert.Equal(
            ["foo", (BigInteger)1, (BigInteger)2, (BigInteger)1],
            signers.Select(signer => worldA0.GetSystemValue(signer.Address)));

        var worldA1 = executionA.Executions[0].Executions[1].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", (BigInteger)2, (BigInteger)1],
            signers.Select(signer => worldA1.GetSystemValue(signer.Address)));

        var worldA2 = executionA.Executions[1].Executions[0].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", "baz", (BigInteger)2],
            signers.Select(signer => worldA2.GetSystemValue(signer.Address)));

        var worldA3 = executionA.Executions[1].Executions[1].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", "baz", "qux"],
            signers.Select(signer => worldA3.GetSystemValue(signer.Address)));

        Assert.IsType<InvalidCastException>(executionA.Executions[1].LeaveExecutions[0].Exception);
        Assert.IsType<InvalidCastException>(executionA.LeaveExecutions[0].Exception);

        var blockExecutedTask = blockchain.BlockExecuted.WaitAsync();
        blockchain.Append(block, blockCommit);
        var executionB = await blockExecutedTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);

        Assert.Equal(2, executionB.Executions.Length);
        Assert.Single(executionB.EnterExecutions);
        Assert.Single(executionB.LeaveExecutions);
        Assert.Equal(4, executionB.Executions.Sum(i => i.Executions.Length));
        Assert.Equal(2, executionB.Executions.Sum(i => i.EnterExecutions.Length));
        Assert.Equal(2, executionB.Executions.Sum(i => i.LeaveExecutions.Length));

        var worldB0 = executionB.Executions[0].Executions[0].LeaveWorld;
        Assert.Equal(
            ["foo", (BigInteger)1, (BigInteger)2, (BigInteger)1],
            signers.Select(signer => worldB0.GetSystemValue(signer.Address)));

        var worldB1 = executionB.Executions[0].Executions[1].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", (BigInteger)2, (BigInteger)1],
            signers.Select(signer => worldB1.GetSystemValue(signer.Address)));

        var worldB2 = executionB.Executions[1].Executions[0].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", "baz", (BigInteger)2],
            signers.Select(signer => worldB2.GetSystemValue(signer.Address)));

        var worldB3 = executionB.Executions[1].Executions[1].LeaveWorld;
        Assert.Equal(
            ["foo", "bar", "baz", "qux"],
            signers.Select(signer => worldB3.GetSystemValue(signer.Address)));

        Assert.IsType<InvalidCastException>(executionB.Executions[1].LeaveExecutions[0].Exception);
        Assert.IsType<InvalidCastException>(executionB.LeaveExecutions[0].Exception);

        Assert.Equal(executionA.StateRootHash, executionB.StateRootHash);
        Assert.True(repositoryA.States.ContainsKey(executionA.StateRootHash));
        Assert.True(repositoryB.States.ContainsKey(executionB.StateRootHash));
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
            SystemAction = new SystemAction
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
        var blockExecutor = new BlockExecutor(repository.States);
        var execution = blockExecutor.Execute(block, options.SystemAction);

        Assert.Equal(2, execution.Executions.Length);
        Assert.Equal(2, execution.EnterExecutions.Length);
        Assert.Equal(2, execution.LeaveExecutions.Length);
        Assert.Equal(4, execution.Executions.Sum(i => i.Executions.Length));
        Assert.Equal(4, execution.Executions.Sum(i => i.EnterExecutions.Length));
        Assert.Equal(4, execution.Executions.Sum(i => i.LeaveExecutions.Length));

        Assert.IsType<ThrowException.SomeException>(execution.EnterExecutions[1].Exception);
        Assert.IsType<ThrowException.SomeException>(execution.LeaveExecutions[1].Exception);
        Assert.IsType<ThrowException.SomeException>(execution.Executions[0].EnterExecutions[1].Exception);
        Assert.IsType<ThrowException.SomeException>(execution.Executions[0].LeaveExecutions[1].Exception);
        Assert.IsType<ThrowException.SomeException>(execution.Executions[1].EnterExecutions[1].Exception);
        Assert.IsType<ThrowException.SomeException>(execution.Executions[1].LeaveExecutions[1].Exception);
    }

    [Fact]
    public void EvaluateWithException()
    {
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Actions = [new ThrowException { ThrowOnExecution = true }],
        }.Create(proposer);
        var repository = new Repository();

        var blockExecutor = new BlockExecutor(repository.States);
        var blockExecution = blockExecutor.Execute(genesisBlock);

        Assert.Empty(blockExecution.EnterExecutions);
        Assert.IsType<Initialize>(blockExecution.Executions[0].Executions[0].Action);
        Assert.IsType<ThrowException.SomeException>(blockExecution.Executions[0].Executions[1].Exception);
        Assert.Empty(blockExecution.LeaveExecutions);
    }

    [Fact]
    public void EvaluateWithCriticalException()
    {
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Actions = [new ThrowException { ThrowOnExecution = true, Deterministic = false, }],
        }.Create(proposer);
        var repository = new Repository();
        var blockExecutor = new BlockExecutor(repository.States);

        Assert.Throws<OutOfMemoryException>(() => blockExecutor.Execute(genesisBlock));
    }

    [Fact]
    public void EvaluateTxs()
    {
        static DumbAction MakeAction(Address address, char identifier, Address? transferTo = null)
        {
            return DumbAction.Create(
                append: (address, identifier.ToString()),
                transfer: transferTo is Address to ? (address, to, 5) : ((Address, Address, BigInteger)?)null);
        }

        var random = new System.Random(0);
        var proposer = RandomUtility.Signer(random);
        var addresses = RandomUtility.Array(random, RandomUtility.Address, 5);
        var repository = new Repository();
        var world = new World(repository.States)
            .SetBalance(addresses[0], DumbAction.DumbCurrency * 100)
            .SetBalance(addresses[1], DumbAction.DumbCurrency * 100)
            .SetBalance(addresses[2], DumbAction.DumbCurrency * 100)
            .SetBalance(addresses[3], DumbAction.DumbCurrency * 100)
            .SetBalance(addresses[4], DumbAction.DumbCurrency * 100)
            .Commit();
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 3);
        var genesisBlock = new GenesisBlockBuilder
        {
            StateRootHash = world.Hash,
        }.Create(proposer);
        var blockExecutor = new BlockExecutor(repository.States);
        repository.Append(genesisBlock, default);
        repository.AppendExecution(blockExecutor.Execute(genesisBlock));

        ImmutableSortedSet<Transaction> blockTxs1 =
        [
            new TransactionBuilder
            {
                Timestamp = DateTimeOffset.MinValue.AddSeconds(1),
                Actions =
                [
                    MakeAction(addresses[0], 'A', addresses[1]),
                    MakeAction(addresses[1], 'B', addresses[2]),
                ]
            }.Create(signers[0]),
            new TransactionBuilder
            {
                Timestamp = DateTimeOffset.MinValue.AddSeconds(2),
                Actions =
                [
                    MakeAction(addresses[2], 'C', addresses[3])
                ]
            }.Create(signers[1]),
            new TransactionBuilder
            {
                Timestamp = DateTimeOffset.MinValue.AddSeconds(3),
            }.Create(signers[2]),
        ];
        var block1 = new BlockBuilder
        {
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = repository.StateRootHash,
            Transactions = blockTxs1,
        }.Create(proposer);
        var blockExecution1 = blockExecutor.Execute(block1);
        var actionExecutions1 = blockExecution1.Executions.SelectMany(exe => exe.Executions).ToArray();

        Assert.Equal(
            [.. blockTxs1],
            blockExecution1.Executions.Select(item => item.Transaction));
        Assert.Equal(3, actionExecutions1.Length);
        Assert.Equal(
            [null, null, "C", null, null],
            addresses.Select(item => actionExecutions1[0].LeaveWorld.GetValueOrDefault(SystemAccount, item)));
        Assert.Equal(
            ["A", null, "C", null, null],
            addresses.Select(item => actionExecutions1[1].LeaveWorld.GetValueOrDefault(SystemAccount, item)));
        Assert.Equal(
            ["A", "B", "C", null, null],
            addresses.Select(item => actionExecutions1[2].LeaveWorld.GetValueOrDefault(SystemAccount, item)));

        Assert.Equal("A", blockExecution1.LeaveWorld.GetSystemValue(addresses[0]));
        Assert.Equal("B", blockExecution1.LeaveWorld.GetSystemValue(addresses[1]));
        Assert.Equal("C", blockExecution1.LeaveWorld.GetSystemValue(addresses[2]));
        Assert.Equal(
            DumbAction.DumbCurrency * 95.0m,
            blockExecution1.LeaveWorld.GetBalance(addresses[0], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 100m,
            blockExecution1.LeaveWorld.GetBalance(addresses[1], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 100m,
            blockExecution1.LeaveWorld.GetBalance(addresses[2], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 105m,
            blockExecution1.LeaveWorld.GetBalance(addresses[3], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 100m,
            blockExecution1.LeaveWorld.GetBalance(addresses[4], DumbAction.DumbCurrency));

        ImmutableSortedSet<Transaction> blockTxs2 =
        [
            new TransactionBuilder
            {
                Timestamp = DateTimeOffset.MinValue.AddSeconds(1),
                Actions =
                [
                    MakeAction(addresses[0], 'D')
                ],
            }.Create(signers[0]),
            new TransactionBuilder
            {
                Timestamp = DateTimeOffset.MinValue.AddSeconds(2),
                Actions =
                [
                    MakeAction(addresses[3], 'E')
                ],
            }.Create(signers[1]),
            new TransactionBuilder
            {
                Timestamp = DateTimeOffset.MinValue.AddSeconds(3),
                Actions =
                [
                    DumbAction.Create((addresses[4], "F"), transfer: (addresses[0], addresses[4], 8)),
                ],
            }.Create(signers[2]),
        ];
        var block2 = new BlockBuilder
        {
            PreviousBlockHash = block1.BlockHash,
            PreviousStateRootHash = blockExecution1.LeaveWorld.Hash,
            Transactions = blockTxs2,
        }.Create(proposer);

        var blockExecution2 = blockExecutor.Execute(block2);
        var actionExecutions2 = blockExecution2.Executions.SelectMany(exe => exe.Executions).ToArray();
        Assert.Equal(
           [.. blockTxs2],
           blockExecution2.Executions.Select(item => item.Transaction));
        Assert.Equal(3, actionExecutions2.Length);
        Assert.Equal(
            ["A", "B", "C", "E", null],
            addresses.Select(item => actionExecutions2[0].LeaveWorld.GetValueOrDefault(SystemAccount, item)));
        Assert.Equal(
            ["A,D", "B", "C", "E", null],
            addresses.Select(item => actionExecutions2[1].LeaveWorld.GetValueOrDefault(SystemAccount, item)));
        Assert.Equal(
            ["A,D", "B", "C", "E", "F"],
            addresses.Select(item => actionExecutions2[2].LeaveWorld.GetValueOrDefault(SystemAccount, item)));

        Assert.Equal("A,D", blockExecution2.LeaveWorld.GetSystemValue(addresses[0]));
        Assert.Equal("B", blockExecution2.LeaveWorld.GetSystemValue(addresses[1]));
        Assert.Equal("C", blockExecution2.LeaveWorld.GetSystemValue(addresses[2]));

        Assert.Equal(
            DumbAction.DumbCurrency * 87.0m,
            blockExecution2.LeaveWorld.GetBalance(addresses[0], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 100m,
            blockExecution2.LeaveWorld.GetBalance(addresses[1], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 100m,
            blockExecution2.LeaveWorld.GetBalance(addresses[2], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 105m,
            blockExecution2.LeaveWorld.GetBalance(addresses[3], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 108m,
            blockExecution2.LeaveWorld.GetBalance(addresses[4], DumbAction.DumbCurrency));
    }

    [Fact]
    public void EvaluateTx()
    {
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var addresses = RandomUtility.Array(random, RandomUtility.Address, 3);
        DumbAction[] actions =
        [
            DumbAction.Create(
                append: (addresses[0], "0"),
                transfer: (addresses[0], addresses[1], 5)),
            DumbAction.Create(
                append: (addresses[1], "1"),
                transfer: (addresses[2], addresses[1], 10)),
            DumbAction.Create(
                append: (addresses[0], "2"),
                transfer: (addresses[1], addresses[0], 10)),
            DumbAction.Create((addresses[2], "R")),
        ];
        var repository = new Repository();
        var world = new World(repository.States)
            .SetBalance(addresses[0], DumbAction.DumbCurrency * 100)
            .SetBalance(addresses[1], DumbAction.DumbCurrency * 100)
            .SetBalance(addresses[2], DumbAction.DumbCurrency * 100)
            .Commit();
        var tx = new TransactionBuilder
        {
            Actions = actions,
        }.Create(proposer);
        var block = new BlockBuilder
        {
            PreviousStateRootHash = world.Hash,
            Transactions = [tx],
        }.Create(proposer);
        var blockExecutor = new BlockExecutor(repository.States);
        var blockExecution = blockExecutor.Execute(block);
        var actionExecutions = blockExecution.Executions.SelectMany(item => item.Executions).ToArray();

        Assert.Equal(actions.Length, actionExecutions.Length);
        string?[][] expectedStates =
        [
            ["0", null, null],
            ["0", "1", null],
            ["0,2", "1", null],
            ["0,2", "1", "R"],
        ];
        BigInteger[][] expectedBalances =
        [
            [95, 105, 100],
            [95, 115, 90],
            [105, 105, 90],
            [105, 105, 90],
        ];

        for (int i = 0; i < actionExecutions.Length; i++)
        {
            var actionExecution2 = actionExecutions[i];
            var actionContext = actionExecution2.ActionContext;

            Assert.Equal(tx.Actions[i], actionExecution2.Action.ToBytecode());
            Assert.Equal(proposer.Address, actionContext.Signer);
            Assert.Equal(tx.Id, actionContext.TxId);
            Assert.Equal(proposer.Address, actionContext.Proposer);
            Assert.Equal(0, actionContext.BlockHeight);
            var world1 = i > 0 ? actionExecutions[i - 1].LeaveWorld : blockExecution.EnterWorld;
            var world2 = actionExecution2.EnterWorld;
            var world3 = actionExecution2.LeaveWorld;
            Assert.Equal(
                addresses.Select(item => world1.GetValueOrDefault(SystemAccount, item)),
                addresses.Select(item => world2.GetValueOrDefault(SystemAccount, item)));
            Assert.Equal(
                expectedStates[i],
                addresses.Select(item => world3.GetValueOrDefault(SystemAccount, item)));
            Assert.Equal(
                expectedBalances[i],
                addresses.Select(item => world3.GetBalance(item, DumbAction.DumbCurrency).RawValue));
        }
    }

    [Fact]
    public void EvaluateTxResultThrowingException()
    {
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var repository = new Repository();
        var block = new BlockBuilder
        {
            Transactions =
            [
                new TransactionBuilder
                {
                    Actions = [new ThrowException { ThrowOnExecution = true }],
                }.Create(proposer),
            ],
        }.Create(proposer);
        var blockExecutor = new BlockExecutor(repository.States);
        var blockExecution = blockExecutor.Execute(block);
        Assert.Equal(new World(repository.States, blockExecution.LeaveWorld.Hash), blockExecution.LeaveWorld);
    }

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
