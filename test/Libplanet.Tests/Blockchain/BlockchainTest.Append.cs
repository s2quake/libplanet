using System.Security.Cryptography;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Serialization;
using Libplanet.Data;
using Libplanet.Types;
using Libplanet.TestUtilities;
using Libplanet.Extensions;
using Libplanet.Data.Structures;
using Libplanet.State.Builtin;

namespace Libplanet.Tests.Blockchain;

public partial class BlockchainTest
{
    [Fact]
    public async Task Append()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(_output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 5);
        var addresses = signers.Select(s => s.Address).ToArray();
        var signer = RandomUtility.Signer(random);

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

        Assert.Single(blockchain.Blocks);
        var block1 = blockchain.Propose(signers[4]);
        var blockExecuted1Task = blockchain.BlockExecuted.WaitAsync();
        blockchain.Append(block1, TestUtils.CreateBlockCommit(block1));
        var blockExecution1 = await blockExecuted1Task.WaitAsync(cancellationToken);
        blockchain.StagedTransactions.AddRange(txs);

        var block2 = blockchain.Propose(signers[4]);
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

        Assert.Equal(
            2,
            blockchain
                .GetWorld()
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(minerAddress));
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

        foreach (var tx in txs)
        {
            var execution = repository.TxExecutions[tx.Id];
            Assert.False(execution.Fail);
            Assert.Equal(block2.BlockHash, execution.BlockHash);
            Assert.Equal(tx.Id, execution.TxId);
        }

        var txExecution1 = repository.TxExecutions[txs[0].Id];
        var outputWorld1 = blockchain
            .GetWorld(Assert.IsType<HashDigest<SHA256>>(txExecution1.OutputState));
        Assert.Equal(
            DumbAction.DumbCurrency * 100,
            outputWorld1.GetBalance(addresses[0], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 100,
            outputWorld1.GetBalance(addresses[1], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 200,
            outputWorld1.GetTotalSupply(DumbAction.DumbCurrency));
        var txExecution2 = repository.TxExecutions[txs[1].Id];
        var outputWorld2 = blockchain
            .GetWorld(Assert.IsType<HashDigest<SHA256>>(txExecution2.OutputState));
        Assert.Equal(
            DumbAction.DumbCurrency * 100,
            outputWorld2.GetBalance(addresses[2], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 100,
            outputWorld2.GetBalance(addresses[3], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 400,
            outputWorld2.GetTotalSupply(DumbAction.DumbCurrency));

        var signer2 = new PrivateKey().AsSigner();
        var tx1Transfer = new TransactionBuilder
        {
            Nonce = 0L,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions =
            [
                DumbAction.Create((signer2.Address, "foo"), (addresses[0], addresses[1], 10)),
                DumbAction.Create((addresses[0], "bar"), (addresses[0], addresses[2], 20)),
            ],
        }.Create(signer2);
        var tx2Error = new TransactionBuilder
        {
            Nonce = 1L,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions =
            [
                // As it tries to transfer a negative value, it throws
                // ArgumentOutOfRangeException:
                DumbAction.Create((signer2.Address, "foo"), (addresses[0], addresses[1], -5)),
            ],
        }.Create(signer2);
        var tx3Transfer = new TransactionBuilder
        {
            Nonce = 2L,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions =
            [
                DumbAction.Create((signer2.Address, "foo"), (addresses[0], addresses[1], 5)),
            ],
        }.Create(signer2);
        blockchain.StagedTransactions.AddRange([tx1Transfer, tx2Error, tx3Transfer]);
        var block3 = blockchain.Propose(signers[4]);
        blockchain.Append(block3, TestUtils.CreateBlockCommit(block3));
        var txExecution3 = repository.TxExecutions[tx1Transfer.Id];
        Assert.False(txExecution3.Fail);
        var inputAccount1 = blockchain.GetWorld(
            Assert.IsType<HashDigest<SHA256>>(txExecution3.InputState))
                .GetAccount(SystemAddresses.SystemAccount);
        var outputWorld3 = blockchain.GetWorld(
            Assert.IsType<HashDigest<SHA256>>(txExecution3.OutputState));
        var outputAccount3 = outputWorld3
                .GetAccount(SystemAddresses.SystemAccount);

        // var accountDiff1 = AccountDiff.Create(inputAccount1, outputAccount1);

        // Assert.Equal(
        //     (new Address[] { addresses[0], pk.Address }).ToImmutableHashSet(),
        //     accountDiff1.StateDiffs.Select(kv => kv.Key).ToImmutableHashSet());
        Assert.Equal(
            "foo",
            outputAccount3.GetValue(signer2.Address));
        Assert.Equal(
            "foo,bar",
            outputAccount3.GetValue(addresses[0]));
        Assert.Equal(
            DumbAction.DumbCurrency * 0,
            outputWorld3.GetBalance(signer2.Address, DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 70,
            outputWorld3.GetBalance(addresses[0], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 110,
            outputWorld3.GetBalance(addresses[1], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 120,
            outputWorld3.GetBalance(addresses[2], DumbAction.DumbCurrency));

        var txExecution4 = repository.TxExecutions[tx2Error.Id];
        Assert.True(txExecution4.Fail);
        Assert.Equal(block3.BlockHash, txExecution4.BlockHash);
        Assert.Equal(tx2Error.Id, txExecution4.TxId);
        Assert.Contains(
            $"{nameof(System)}.{nameof(ArgumentOutOfRangeException)}",
            txExecution4.ExceptionNames);

        var txExecution5 = repository.TxExecutions[tx3Transfer.Id];
        Assert.False(txExecution5.Fail);
        var outputWorld5 = blockchain.GetWorld(
            Assert.IsType<HashDigest<SHA256>>(txExecution5.OutputState));
        Assert.Equal(
            DumbAction.DumbCurrency * 0,
            outputWorld5.GetBalance(signer2.Address, DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 65,
            outputWorld5.GetBalance(addresses[0], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 115,
            outputWorld5.GetBalance(addresses[1], DumbAction.DumbCurrency));
    }

    [Fact]
    public void AppendModern()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        var accountAddress = Address.Parse("0123456789abcdef0123456789abcdef12345678");
        var address1 = RandomUtility.Address(random);
        var address2 = RandomUtility.Address(random);
        var action1 = DumbAction.Create((address1, "foo")) with { AccountAddress = accountAddress };
        var action2 = DumbAction.Create((address2, "bar")) with { AccountAddress = accountAddress };

        blockchain.StagedTransactions.Add(proposer, new() { Actions = [action1] });
        blockchain.StagedTransactions.Add(proposer, new() { Actions = [action2] });
        blockchain.ProposeAndAppend(proposer);

        var world1 = blockchain.GetWorld();
        Assert.Equal(
            "foo",
            world1.GetAccount(accountAddress).GetValue(address1));
        var block2 = blockchain.Propose(proposer);
        blockchain.Append(block2, TestUtils.CreateBlockCommit(block2));
        var world2 = blockchain.GetWorld();
        Assert.Equal(
            "bar",
            world2.GetAccount(accountAddress).GetValue(address2));
    }

    [Fact]
    public void AppendFailDueToInvalidBytesLength()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var options = new BlockchainOptions
        {
        };
        var blockchain = new Libplanet.Blockchain(genesisBlock, options);

        var manyActions = Enumerable.Repeat(DumbAction.Create((default, "_")), 200).ToArray();
        var signer = RandomUtility.Signer(random);
        int nonce = 0;
        var txList = new List<Transaction>();
        for (var i = 0; i < 100; i++)
        {
            if (i % 25 == 0)
            {
                nonce = 0;
                signer = RandomUtility.Signer(random);
            }

            var tx = new TransactionBuilder
            {
                Nonce = nonce,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions = manyActions,
            }.Create(signer);
            txList.Add(tx);
            nonce++;
        }

        var block = new BlockBuilder
        {
            Height = 1,
            PreviousHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [.. txList],
        }.Create(proposer);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        var maxBytes = options.BlockOptions.MaxActionBytes;
        Assert.True(ModelSerializer.SerializeToBytes(block).Length > maxBytes);

        var e = Assert.Throws<ArgumentException>(() => blockchain.Append(block, blockCommit));
        Assert.StartsWith("The size of block", e.Message);
    }

    [Fact]
    public void AppendFailDueToInvalidTxCount()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var options = new BlockchainOptions
        {
        };
        var blockchain = new Libplanet.Blockchain(genesisBlock, options);

        var maxTxs = options.BlockOptions.MaxTransactions;
        for (var i = 0; i <= maxTxs; i++)
        {
            blockchain.StagedTransactions.Add(proposer, new() { });
        }

        Assert.True(blockchain.StagedTransactions.Count > maxTxs);

        var block = new BlockBuilder
        {
            Height = 1,
            PreviousHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [.. blockchain.StagedTransactions.Values],
        }.Create(proposer);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        Assert.True(block.Transactions.Count > maxTxs);

        var e = Assert.Throws<ArgumentException>(() => blockchain.Append(block, blockCommit));
        Assert.Contains("should include at most", e.Message);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task AppendWhenActionEvaluationFailed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var options = new BlockchainOptions
        {
        };
        var blockchain = new Libplanet.Blockchain(genesisBlock, options);
        var signer = RandomUtility.Signer(random);

        var action = new ThrowException { ThrowOnExecution = true };
        blockchain.StagedTransactions.Add(signer, @params: new() { Actions = [action] });

        var blockExecutedTask = blockchain.BlockExecuted.WaitAsync();
        blockchain.ProposeAndAppend(signer);
        var blockExecution = await blockExecutedTask.WaitAsync(cancellationToken);

        Assert.Equal(2, blockchain.Blocks.Count);
        Assert.IsType<ThrowException.SomeException>(blockExecution.Executions.Single().Executions.Single().Exception);
    }

    [Fact]
    public void AppendBlockWithPolicyViolationTx()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var validSigner = RandomUtility.Signer(random);
        var invalidSigner = RandomUtility.Signer(random);
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(tx =>
                    {
                        var validAddress = validSigner.Address;
                        if (tx.Signer != validAddress && tx.Signer != proposer.Address)
                        {
                            throw new InvalidOperationException("invalid signer");
                        }
                    }),
                ],
            },
        };
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock, options);

        _ = blockchain.StagedTransactions.Add(validSigner, new());

        blockchain.ProposeAndAppend(proposer);

        _ = blockchain.StagedTransactions.Add(invalidSigner, new());

        var e = Assert.Throws<InvalidOperationException>(() => blockchain.ProposeAndAppend(proposer));
        Assert.Equal("invalid signer", e.Message);
    }

    [Fact]
    public void UnstageAfterAppendComplete()
    {
        var random = RandomUtility.GetRandom(_output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 5);
        var addresses = signers.Select(s => s.Address).ToArray();
        var signer = RandomUtility.Signer(random);

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

        var signerA = RandomUtility.Signer(random);
        Assert.Empty(blockchain.StagedTransactions.Keys);

        // Mining with empty staged.
        _ = blockchain.ProposeAndAppend(signerA);
        Assert.Empty(blockchain.StagedTransactions.Keys);

        blockchain.StagedTransactions.AddRange(txs);
        Assert.Equal(2, blockchain.StagedTransactions.Keys.Count());

        // Tx with nonce 0 is mined.
        var block2 = new BlockBuilder
        {
            Height = blockchain.Tip.Height + 1,
            PreviousHash = blockchain.Tip.BlockHash,
            PreviousCommit = blockchain.BlockCommits[blockchain.Tip.BlockHash],
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [txs[0]],
        }.Create(signerA);
        var blockCommit2 = TestUtils.CreateBlockCommit(block2);
        blockchain.Append(block2, blockCommit2);
        Assert.Single(blockchain.StagedTransactions.Keys);

        // Two txs with nonce 1 are staged.
        var actions = new[] { DumbAction.Create((addresses[0], "foobar")) };
        var tx2 = new TransactionBuilder
        {
            Nonce = 1L,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions = actions,
        }.Create(signer);
        blockchain.StagedTransactions.Add(tx2);
        Assert.Equal(2, blockchain.StagedTransactions.Keys.Count());

        // Unmined tx is left intact in the stage.
        var block3 = new BlockBuilder
        {
            Height = blockchain.Tip.Height + 1,
            PreviousHash = blockchain.Tip.BlockHash,
            PreviousCommit = blockchain.BlockCommits[blockchain.Tip.BlockHash],
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [txs[1]],
        }.Create(signerA);
        var blockCommit3 = TestUtils.CreateBlockCommit(block3);
        blockchain.Append(block3, blockCommit3);
        var tx3 = Assert.Single(blockchain.StagedTransactions.Values);
        Assert.Equal(tx2, tx3);
        blockchain.StagedTransactions.Prune();
        Assert.Empty(blockchain.StagedTransactions.Values);
    }

    [Fact]
    public void AppendValidatesBlock()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var options = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Block>(block =>
                    {
                        throw new InvalidOperationException("block");
                    }),
                ],
            },
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(tx =>
                    {
                        throw new InvalidOperationException("tx");
                    }),
                ],
            },
        };
        var blockchain = new Libplanet.Blockchain(genesisBlock, options);
        var block1 = blockchain.Propose(proposer);
        var blockCommit1 = TestUtils.CreateBlockCommit(block1);
        var e = Assert.Throws<InvalidOperationException>(() => blockchain.Append(block1, blockCommit1));
        Assert.Equal("block", e.Message);
    }

    [Fact]
    public void AppendWithdrawTxsWithExpiredNoncesFromStage()
    {
        var random = RandomUtility.GetRandom(_output);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        var txA0 = new TransactionBuilder
        {
            Nonce = 0,
            GenesisBlockHash = genesisBlock.BlockHash,
        }.Create(signerA);
        var txA1 = new TransactionBuilder
        {
            Nonce = 1,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions = [],
        }.Create(signerA);
        blockchain.StagedTransactions.Add(txA0);
        blockchain.StagedTransactions.Add(txA1);
        var block = blockchain.Propose(signerA);

        var txA2 = new TransactionBuilder
        {
            Nonce = 2,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions = [],
        }.Create(signerA);
        var txA0_ = new TransactionBuilder
        {
            Nonce = 0,
            GenesisBlockHash = genesisBlock.BlockHash,
        }.Create(signerA);
        var txA1_ = new TransactionBuilder
        {
            Nonce = 1,
            GenesisBlockHash = genesisBlock.BlockHash,
        }.Create(signerA);
        var txB0 = new TransactionBuilder
        {
            Nonce = 1,
            GenesisBlockHash = genesisBlock.BlockHash,
        }.Create(signerB);
        var txB1 = new TransactionBuilder
        {
            Nonce = 1,
            GenesisBlockHash = genesisBlock.BlockHash,
        }.Create(signerB);
        var txB2 = new TransactionBuilder
        {
            Nonce = 2,
            GenesisBlockHash = genesisBlock.BlockHash,
        }.Create(signerB);
        var txB0_ = new TransactionBuilder
        {
            Nonce = 1,
            GenesisBlockHash = genesisBlock.BlockHash,
        }.Create(signerB);
        var txB1_ = new TransactionBuilder
        {
            Nonce = 1,
            GenesisBlockHash = genesisBlock.BlockHash,
        }.Create(signerB);
        blockchain.StagedTransactions.Add(txA2);
        blockchain.StagedTransactions.Add(txA0_);
        blockchain.StagedTransactions.Add(txA1_);
        blockchain.StagedTransactions.Add(txB0);
        blockchain.StagedTransactions.Add(txB1);
        blockchain.StagedTransactions.Add(txB2);
        blockchain.StagedTransactions.Add(txB0_);
        blockchain.StagedTransactions.Add(txB1_);
        Assert.Equal(
            new Transaction[]
            {
                txA0, txA1, txA2, txA0_, txA1_, txB0, txB1, txB2, txB0_, txB1_,
            }.Select(tx => tx.Id).ToImmutableSortedSet(),
            [.. blockchain.StagedTransactions.Keys]);

        blockchain.Append(block, TestUtils.CreateBlockCommit(block));
        Assert.Equal(
            new Transaction[]
            {
                txA2, txA0_, txA1_, txB0, txB1, txB2, txB0_, txB1_,
            }.Select(tx => tx.Id).ToImmutableSortedSet(),
            [.. blockchain.StagedTransactions.Keys]);
        blockchain.StagedTransactions.Prune();
        Assert.Equal(
            new Transaction[]
            {
                txA2, txB0, txB1, txB2, txB0_, txB1_,
            }.Select(tx => tx.Id).ToImmutableSortedSet(),
            [.. blockchain.StagedTransactions.Keys]);
    }

    [Fact]
    public void DoesNotMigrateStateWithoutAction()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var options = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                MaxActionBytes = 50 * 1024,
            },
        };

        var txs = new[]
        {
            new TransactionMetadata
            {
                Nonce = 0,
                Signer = proposer.Address,
                Timestamp = DateTimeOffset.UtcNow,
                Actions = new[]
                {
                    new Initialize
                    {
                        Validators = TestUtils.Validators,
                    },
                }.ToBytecodes(),
            }.Sign(proposer),
        };
        var evs = Array.Empty<EvidenceBase>();
        var rawGenesisBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 0,
                Timestamp = DateTimeOffset.UtcNow,

                Proposer = proposer.Address,
            },
            Content = new BlockContent
            {
                Transactions = [.. txs],
                Evidences = [.. evs],
            },
        };
        var genesisBlock = rawGenesisBlock.Sign(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock, options);
        var (block1, _) = blockchain.ProposeAndAppend(proposer);
        Assert.Equal<byte>(
            blockchain.GetWorld(genesisBlock.BlockHash).Trie.Hash.Bytes,
            blockchain.GetWorld(block1.PreviousStateRootHash).Trie.Hash.Bytes);
    }

    // [Fact]
    // public void AppendSRHPostponeBPVBump()
    // {
    //     var beforePostponeBPV = BlockHeader.CurrentProtocolVersion - 1;
    //     var options = new BlockchainOptions();
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

    //     var blockchain = TestUtils.MakeBlockchain(
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
    //     blockchain.Append(blockBeforeBump, commitBeforeBump);

    //     // Append block after state root hash postpone - previous block is not bumped
    //     action = DumbAction.Create((new Address([.. RandomUtility.Bytes(20)]), "bar"));
    //     tx = new TransactionMetadata
    //     {
    //         Nonce = 1,
    //         Signer = proposer.Address,
    //         GenesisHash = genesis.BlockHash,
    //         Actions = new[] { action }.ToBytecodes(),
    //     }.Sign(proposer);
    //     var blockAfterBump1 = blockchain.ProposeBlock(proposer);
    //     Assert.Equal(
    //         BlockHeader.CurrentProtocolVersion,
    //         blockAfterBump1.Version);
    //     var commitAfterBump1 = TestUtils.CreateBlockCommit(blockAfterBump1);
    //     blockchain.Append(blockAfterBump1, commitAfterBump1);
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
    //     var blockAfterBump2 = blockchain.ProposeBlock(proposer);
    //     Assert.Equal(
    //         BlockHeader.CurrentProtocolVersion,
    //         blockAfterBump2.Version);
    //     var commitAfterBump2 = TestUtils.CreateBlockCommit(blockAfterBump2);
    //     blockchain.Append(blockAfterBump2, commitAfterBump2);
    //     Assert.Equal(
    //         blockExecutor.Evaluate(
    //             (RawBlock)blockAfterBump1, blockAfterBump1.StateRootHash)[^1].OutputWorld.Trie.Hash,
    //         blockAfterBump2.StateRootHash);
    // }
}
