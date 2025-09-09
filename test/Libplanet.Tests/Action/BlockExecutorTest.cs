using Libplanet.Builtin;
using Libplanet.Data;
using Libplanet.Extensions;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Actions;
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
        var random = Rand.GetRandom(output);
        var signer = Rand.Signer(random);
        var proposer = Rand.Signer(random);
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
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var signer = Rand.Signer(random);

        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
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
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var repositoryB = new Repository();
        var signers = Rand.Array(random, Rand.Signer, 4);
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
        var random = Rand.GetRandom(output);
        var signers = Rand.Array(random, Rand.Signer, 4);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
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
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var genesisBlockBuilder = TestUtils.GenesisBlockBuilder with
        {
            Actions = [new ThrowException { ThrowOnExecution = true }],
        };
        var genesisBlock = genesisBlockBuilder.Create(proposer);
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
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var genesisBlockBuilder = TestUtils.GenesisBlockBuilder with
        {
            Actions = [new ThrowException { ThrowOnExecution = true, Deterministic = false, }],
        };
        var genesisBlock = genesisBlockBuilder.Create(proposer);
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
        var proposer = Rand.Signer(random);
        var addresses = Rand.Array(random, Rand.Address, 5);
        var repository = new Repository();
        var world = new World(repository.States)
            .SetBalance(addresses[0], DumbAction.DumbCurrency * 100)
            .SetBalance(addresses[1], DumbAction.DumbCurrency * 100)
            .SetBalance(addresses[2], DumbAction.DumbCurrency * 100)
            .SetBalance(addresses[3], DumbAction.DumbCurrency * 100)
            .SetBalance(addresses[4], DumbAction.DumbCurrency * 100)
            .Commit();
        var signers = Rand.Array(random, Rand.Signer, 3);
        var genesisBlockBuilder = TestUtils.GenesisBlockBuilder with
        {
            StateRootHash = world.Hash,
        };
        var genesisBlock = genesisBlockBuilder.Create(proposer);
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
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var addresses = Rand.Array(random, Rand.Address, 3);
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
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
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
}
