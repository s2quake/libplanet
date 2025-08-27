using Libplanet.Data;
using Libplanet.Extensions;
using Libplanet.State;
using Libplanet.State.Builtin;
using Libplanet.State.Tests.Actions;
using Libplanet.Tests.Store;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Xunit.Internal;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests;

public partial class BlockchainTest(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void BaseTest()
    {
        var blockchain = new Blockchain();
        Assert.NotEqual(Guid.Empty, blockchain.Id);
        Assert.Empty(blockchain.Blocks);
        Assert.Empty(blockchain.BlockCommits);
        Assert.Empty(blockchain.StagedTransactions);
        Assert.Empty(blockchain.Transactions);
        Assert.Empty(blockchain.PendingEvidence);
        Assert.Empty(blockchain.Evidence);
        Assert.Empty(blockchain.TxExecutions);
        Assert.Throws<InvalidOperationException>(() => blockchain.StateRootHash);
        Assert.Throws<InvalidOperationException>(() => blockchain.BlockCommit);
        Assert.Throws<InvalidOperationException>(() => blockchain.Genesis);
        Assert.Throws<InvalidOperationException>(() => blockchain.Tip);
    }

    [Fact]
    public void BaseTest_WithGenesis()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new BlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        Assert.NotEqual(Guid.Empty, blockchain.Id);
        Assert.Single(blockchain.Blocks);
        Assert.Single(blockchain.BlockCommits);
        Assert.Empty(blockchain.StagedTransactions);
        Assert.Empty(blockchain.Transactions);
        Assert.Empty(blockchain.PendingEvidence);
        Assert.Empty(blockchain.Evidence);
        Assert.Empty(blockchain.TxExecutions);
        Assert.Equal(default, blockchain.StateRootHash);
        Assert.Equal(default, blockchain.BlockCommit);
        Assert.Equal(genesisBlock, blockchain.Genesis);
        Assert.Equal(genesisBlock, blockchain.Tip);
    }

    [Fact]
    public void BaseTest_WithGenesis_WithTransaction()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var action = new Initialize
        {
            Validators = [new Validator { Address = proposer.Address }],
        };
        var genesisBlock = new BlockBuilder
        {
            Transactions =
            [
                new TransactionBuilder
                {
                    Actions = [action],
                }.Create(proposer),
            ],
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        Assert.NotEqual(Guid.Empty, blockchain.Id);
        Assert.Single(blockchain.Blocks);
        Assert.Single(blockchain.BlockCommits);
        Assert.Empty(blockchain.StagedTransactions);
        Assert.Single(blockchain.Transactions);
        Assert.Empty(blockchain.PendingEvidence);
        Assert.Empty(blockchain.Evidence);
        Assert.Single(blockchain.TxExecutions);
        Assert.NotEqual(default, blockchain.StateRootHash);
        Assert.Equal(default, blockchain.BlockCommit);
        Assert.Equal(genesisBlock, blockchain.Genesis);
        Assert.Equal(genesisBlock, blockchain.Tip);
    }

    [Fact]
    public void Validators()
    {
        var random = RandomUtility.GetRandom(_output);
        var validators = RandomUtility.Array(random, RandomUtility.Validator, 4);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = [..validators],
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var actualValidators = blockchain.GetWorld().GetValidators();
        Assert.Equal(validators, actualValidators);
    }

    [Fact]
    public void CanFindBlockByHeight()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new BlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        var genesis = blockchain.Genesis;
        Assert.Equal(genesis, blockchain.Blocks[0]);
        Assert.Equal(default, blockchain.BlockCommits[0]);

        var (block, blockCommit) = blockchain.ProposeAndAppend(proposer);
        Assert.Equal(block, blockchain.Blocks[1]);
        Assert.Equal(blockCommit, blockchain.BlockCommits[1]);
    }

    [Fact]
    public void BlockHashes()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new BlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        Assert.Single(blockchain.Blocks.Keys);

        var (block1, _) = blockchain.ProposeAndAppend(proposer);
        Assert.Equal([genesisBlock.BlockHash, block1.BlockHash], blockchain.Blocks.Keys);

        var (block2, _) = blockchain.ProposeAndAppend(proposer);
        Assert.Equal(
            [genesisBlock.BlockHash, block1.BlockHash, block2.BlockHash],
            blockchain.Blocks.Keys);

        var (block3, _) = blockchain.ProposeAndAppend(proposer);
        Assert.Equal(
            [genesisBlock.BlockHash, block1.BlockHash, block2.BlockHash, block3.BlockHash],
            blockchain.Blocks.Keys);
    }

    [Fact]
    public void ProcessActions()
    {
        var random = RandomUtility.GetRandom(_output);
        var address1 = RandomUtility.Address(random);
        var actions = new IAction[]
        {
            new Initialize { Validators = TestUtils.Validators, },
        };

        var genesisBlock = new BlockBuilder
        {
            Transactions =
            [
                new TransactionBuilder
                {
                    Actions = actions,
                }.Create(GenesisProposer),
            ],
        }.Create(GenesisProposer);

        var blockchain = new Blockchain(genesisBlock);

        IAction[] actions1 =
        [
            new Attack
            {
                Weapon = "sword",
                Target = "goblin",
                TargetAddress = address1,
            },
            new Attack
            {
                Weapon = "sword",
                Target = "orc",
                TargetAddress = address1,
            },
            new Attack
            {
                Weapon = "staff",
                Target = "goblin",
                TargetAddress = address1,
            },
        ];
        var tx1Signer = RandomUtility.Signer(random);
        var tx1 = new TransactionMetadata
        {
            Signer = tx1Signer.Address,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions = actions1.ToBytecodes(),
        }.Sign(tx1Signer);

        blockchain.StagedTransactions.Add(tx1);
        _ = blockchain.ProposeAndAppend(RandomUtility.Signer(random));
        var result = (BattleResult)blockchain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(address1);

        Assert.Contains("sword", result.UsedWeapons);
        Assert.Contains("staff", result.UsedWeapons);
        Assert.Contains("orc", result.Targets);
        Assert.Contains("goblin", result.Targets);

        IAction[] actions2 =
        [
            new Attack
            {
                Weapon = "bow",
                Target = "goblin",
                TargetAddress = address1,
            },
        ];
        var tx2Signer = RandomUtility.Signer(random);
        var tx2 = new TransactionMetadata
        {
            Signer = tx2Signer.Address,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions = actions2.ToBytecodes(),
        }.Sign(tx2Signer);

        blockchain.StagedTransactions.Add(tx2);
        _ = blockchain.ProposeAndAppend(RandomUtility.Signer(random));

        result = (BattleResult)blockchain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(address1);

        Assert.Contains("bow", result.UsedWeapons);

        var tx3Signer = RandomUtility.Signer(random);
        var tx3 = new TransactionMetadata
        {
            Signer = tx3Signer.Address,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions = new[]
            {
                new Attack
                {
                    Weapon = "sword",
                    Target = "orc",
                    TargetAddress = address1,
                },
            }.ToBytecodes(),
        }.Sign(tx3Signer);
        var block3 = blockchain.Propose(RandomUtility.Signer(random));
        blockchain.StagedTransactions.Add(tx3);
        blockchain.Append(block3, CreateBlockCommit(block3));
        result = (BattleResult)blockchain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(address1);

    }

    [Fact]
    public async Task ActionRenderersHaveDistinctContexts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(_output);
        var options = new BlockchainOptions();
        var blockchain = MakeBlockchain(options);
        var signer = RandomUtility.Signer(random);
        blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [DumbAction.Create((default, string.Empty))],
        });
        var block = blockchain.Propose(RandomUtility.Signer(random));
        var blockCommit = CreateBlockCommit(block);
        var actionExecutedTask = blockchain.ActionExecuted.WaitAsync();

        blockchain.Append(block, blockCommit);

        var info = await actionExecutedTask.WaitAsync(cancellationToken);
        Assert.IsType<DumbAction>(info.Action);
    }

    [Fact]
    public async Task RenderActionsAfterBlockIsRendered()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(_output);
        var options = new BlockchainOptions();
        var blockchain = MakeBlockchain(options);
        var signer = RandomUtility.Signer(random);

        var action = DumbAction.Create((default, string.Empty));
        var actions = new[] { action };
        blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = actions,
        });

        var actionExecutedTask = blockchain.ActionExecuted.WaitAsync();
        var blockExecutedTask = blockchain.BlockExecuted.WaitAsync();
        var (block, _) = blockchain.ProposeAndAppend(RandomUtility.Signer(random));
        var actionInfo = await actionExecutedTask.WaitAsync(cancellationToken);
        var blockInfo = await blockExecutedTask.WaitAsync(cancellationToken);

        Assert.Equal(2, blockchain.Blocks.Count);
        Assert.Equal(block.Header, blockInfo.Block.Header);
        Assert.Equal(block.Content, blockInfo.Block.Content);
        Assert.Equal(action, actionInfo.Action);
    }

    [Fact]
    public void RenderActionsAfterAppendComplete()
    {
        var random = RandomUtility.GetRandom(_output);
        var blockchain = MakeBlockchain();
        var signer = RandomUtility.Signer(random);

        var action = DumbAction.Create((default, string.Empty));
        blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [action],
        });
        using var _ = blockchain.ActionExecuted.Subscribe(e =>
        {
            if (e.Action is DumbAction)
            {
                throw new ThrowException.SomeException("thrown by renderer");
            }
        });

        var e = Assert.Throws<ThrowException.SomeException>(
            () => blockchain.ProposeAndAppend(RandomUtility.Signer(random)));
        Assert.Equal("thrown by renderer", e.Message);
        Assert.Equal(2, blockchain.Blocks.Count);
    }

    [Fact]
    public void FindNextHashes()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var blockchain = MakeBlockchain();

        Assert.Single(blockchain.BlockHashes[0..], blockchain.Genesis.BlockHash);
        var block0 = blockchain.Genesis;
        var (block1, _) = blockchain.ProposeAndAppend(signer);
        var (block2, _) = blockchain.ProposeAndAppend(signer);
        var (block3, _) = blockchain.ProposeAndAppend(signer);

        Assert.Equal(
            [block0.BlockHash, block1.BlockHash, block2.BlockHash, block3.BlockHash],
            blockchain.BlockHashes[0..]);

        Assert.Equal(
            [block1.BlockHash, block2.BlockHash, block3.BlockHash],
            blockchain.BlockHashes[1..]);

        Assert.Equal(
            [block0.BlockHash, block1.BlockHash],
            blockchain.BlockHashes[0..2]);
    }

    [Fact]
    public void DetectInvalidTxNonce()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var signer = RandomUtility.Signer(random);
        var address1 = RandomUtility.Address(random);
        var actions = new[] { DumbAction.Create((address1, "foo")) };
        var blockchain = MakeBlockchain();

        var tx1 = blockchain.StagedTransactions.Add(signer, new() { Actions = actions });
        var (block1, blockCommit1) = blockchain.ProposeAndAppend(proposer);

        var block2 = new BlockBuilder
        {
            Height = 2,
            PreviousBlockHash = block1.BlockHash,
            PreviousBlockCommit = blockCommit1,
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [tx1],
        }.Create(proposer);
        var blockCommit2 = CreateBlockCommit(block2);

        var e = Assert.Throws<ArgumentException>("block", () => blockchain.Append(block2, blockCommit2));
        Assert.Contains("has an invalid nonce", e.Message);

        var tx3 = new TransactionBuilder
        {
            Nonce = 1,
            GenesisBlockHash = blockchain.Genesis.BlockHash,
            Actions = actions,
        }.Create(signer);
        var block3 = new BlockBuilder
        {
            Height = 2,
            PreviousBlockHash = block1.BlockHash,
            PreviousBlockCommit = blockCommit1,
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [tx3],
        }.Create(proposer);
        var blockCommit3 = CreateBlockCommit(block3);
        blockchain.Append(block3, blockCommit3);
    }

    [Fact]
    public void GetBlockLocator()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var blockchain = MakeBlockchain();
        var items = blockchain.ProposeAndAppendMany(signer, 10);
        var actual = blockchain.Tip.BlockHash;
        var expected = items[9].Block.BlockHash;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetBlockCommit()
    {
        var random = RandomUtility.GetRandom(_output);
        var blockchain = MakeBlockchain();
        Assert.Equal(default, blockchain.BlockCommits[0]);
        Assert.Equal(default, blockchain.BlockCommits[blockchain.Genesis.BlockHash]);

        var (block1, blockCommit1) = blockchain.ProposeAndAppend(RandomUtility.Signer(random));

        Assert.Equal(blockCommit1, blockchain.BlockCommits[block1.Height]);
        Assert.Equal(blockCommit1, blockchain.BlockCommits[block1.BlockHash]);

        var block2 = new BlockBuilder
        {
            Height = 2,
            PreviousBlockHash = block1.BlockHash,
            PreviousBlockCommit = CreateBlockCommit(blockchain.Tip),
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [],
        }.Create(RandomUtility.Signer(random));
        var blockCommit2 = CreateBlockCommit(block2);
        blockchain.Append(block2, blockCommit2);

        Assert.Equal(blockCommit2, blockchain.BlockCommits[block2.Height]);
        Assert.NotEqual(block2.PreviousBlockCommit, blockchain.BlockCommits[block1.Height]);
        Assert.NotEqual(block2.PreviousBlockCommit, blockchain.BlockCommits[block1.BlockHash]);
    }

    [Fact]
    public void CleanupBlockCommitStore()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var repository = new Repository();
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        _ = new Blockchain(genesisBlock, repository);
        var blockCommit1 = CreateBlockCommit(RandomUtility.BlockHash(random), 1, 0);
        var blockCommit2 = CreateBlockCommit(RandomUtility.BlockHash(random), 2, 0);
        var blockCommit3 = CreateBlockCommit(RandomUtility.BlockHash(random), 3, 0);

        repository.BlockCommits.Add(blockCommit1);
        repository.BlockCommits.Add(blockCommit2);
        repository.BlockCommits.Add(blockCommit3);
        repository.BlockCommits.Prune(blockCommit3.Height);

        Assert.Throws<KeyNotFoundException>(() => repository.BlockCommits[blockCommit1.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockCommits[blockCommit2.BlockHash]);
        Assert.Equal(blockCommit3, repository.BlockCommits[blockCommit3.BlockHash]);
    }

    [Fact]
    public void GetStatesOnCreatingBlockChain()
    {
        var random = RandomUtility.GetRandom(_output);
        var invoked = false;
        var options = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Block>(obj => invoked = true),
                ],
            },
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(obj => invoked = true),
                ],
            },
        };
        var genesisProposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(genesisProposer);

        _ = new Blockchain(genesisBlock, options);
        Assert.False(invoked);
    }

    [Fact]
    public void GetStateReturnsLatestStatesWhenMultipleAddresses()
    {
        var random = RandomUtility.GetRandom(_output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 10);
        var addresses = signers.Select(signer => signer.Address).ToList();
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);

        var blockchain = new Blockchain(genesisBlock);

        foreach (var address in addresses)
        {
            var world = blockchain.GetWorld();
            var account = world.GetAccount(SystemAddresses.SystemAccount);
            Assert.Throws<KeyNotFoundException>(() => account.GetValue(address));
            Assert.Null(account.GetValueOrDefault(address));
        }

        foreach (var signer in signers)
        {
            blockchain.StagedTransactions.Add(signer, @params: new()
            {
                Actions = [DumbAction.Create((signer.Address, "1"))],
            });
        }

        _ = blockchain.ProposeAndAppend(signers[0]);

        foreach (var address in addresses)
        {
            var world = blockchain.GetWorld();
            var account = world.GetAccount(SystemAddresses.SystemAccount);
            Assert.Equal("1", account.GetValue(address));
        }

        blockchain.StagedTransactions.Add(signers[0], @params: new()
        {
            Actions = [DumbAction.Create((addresses[0], "2"))],
        });
        _ = blockchain.ProposeAndAppend(signers[0]);

        Assert.Equal("1,2", blockchain.GetWorld().GetAccount(SystemAddresses.SystemAccount).GetValue(addresses[0]));
        for (var i = 1; i < addresses.Count; i++)
        {
            var world = blockchain.GetWorld();
            var account = world.GetAccount(SystemAddresses.SystemAccount);
            Assert.Equal("1", account.GetValue(addresses[i]));
        }
    }

    [Fact]
    public void FindBranchPoint()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var genesisProposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(genesisProposer);
        var blockchain = new Blockchain(genesisBlock);

        var (b1, _) = blockchain.ProposeAndAppend(signer);
        var (b2, _) = blockchain.ProposeAndAppend(signer);
        _ = blockchain.ProposeAndAppend(signer);
        var (b4, _) = blockchain.ProposeAndAppend(signer);

        Assert.Equal(b1.PreviousBlockHash, blockchain.Genesis.BlockHash);

        var emptyLocator = blockchain.Genesis.BlockHash;
        var invalidLocator = RandomUtility.BlockHash(random);
        var locator = b4.BlockHash;

        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
        blockchainB.Append(b1, CreateBlockCommit(b1));
        blockchainB.Append(b2, CreateBlockCommit(b2));
        _ = blockchainB.ProposeAndAppend(signer);

        // Testing blockchainA
        Assert.Contains(emptyLocator, blockchainA.Blocks.Keys);
        Assert.DoesNotContain(invalidLocator, blockchainA.Blocks.Keys);
        Assert.DoesNotContain(locator, blockchainA.Blocks.Keys);

        // Testing blockchain
        Assert.Contains(emptyLocator, blockchain.Blocks.Keys);
        Assert.DoesNotContain(invalidLocator, blockchain.Blocks.Keys);
        Assert.Contains(locator, blockchain.Blocks.Keys);

        // Testing blockchainB
        Assert.Contains(emptyLocator, blockchainB.Blocks.Keys);
        Assert.DoesNotContain(invalidLocator, blockchainB.Blocks.Keys);
        Assert.DoesNotContain(locator, blockchainB.Blocks.Keys);
    }

    [Fact]
    public void GetNextTxNonce()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var address = signer.Address;
        var addressA = RandomUtility.Address(random);
        var actions = new[] { DumbAction.Create((addressA, "foo")) };

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var genesisBlockHash = genesisBlock.BlockHash;
        var blockchain = new Blockchain(genesisBlock);
        var txBuilder = new TransactionBuilder
        {
            GenesisBlockHash = genesisBlockHash,
            Actions = actions,
        };

        Assert.Equal(0L, blockchain.GetNextTxNonce(address));

        Transaction[] txsA =
        [
            (txBuilder with { Nonce = 0L }).Create(signer),
        ];

        blockchain.StagedTransactions.AddRange(txsA);
        _ = blockchain.ProposeAndAppend(proposer);

        Assert.Equal(1L, blockchain.GetNextTxNonce(address));

        Transaction[] txsB =
        [
            (txBuilder with { Nonce = 1L }).Create(signer),
            (txBuilder with { Nonce = 2L }).Create(signer),
        ];

        blockchain.StagedTransactions.AddRange(txsB);

        Assert.Equal(3L, blockchain.GetNextTxNonce(address));

        Transaction[] txsC =
        [
            (txBuilder with { Nonce = 3L }).Create(signer),
            (txBuilder with { Nonce = 3L }).Create(signer),
        ];
        blockchain.StagedTransactions.AddRange(txsC);

        Assert.Equal(4L, blockchain.GetNextTxNonce(address));

        Transaction[] txsD =
        [
            (txBuilder with { Nonce = 5L }).Create(signer),
        ];
        blockchain.StagedTransactions.AddRange(txsD);

        Assert.Equal(4L, blockchain.GetNextTxNonce(address));

        Transaction[] txsE =
        [
            (txBuilder with { Nonce = 4L }).Create(signer),
            (txBuilder with { Nonce = 5L }).Create(signer),
            (txBuilder with { Nonce = 7L }).Create(signer),
        ];
        blockchain.StagedTransactions.AddRange(txsE);

        Assert.Equal(6L, blockchain.GetNextTxNonce(address));
    }

    [Fact]
    public void GetNextTxNonceWithStaleTx()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var address = signer.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var genesisBlockHash = genesisBlock.BlockHash;
        var blockchain = new Blockchain(genesisBlock);
        var txBuilder = new TransactionBuilder
        {
            GenesisBlockHash = genesisBlockHash,
            Actions = actions,
        };

        Transaction[] txs =
        [
            (txBuilder with { Nonce = 0L }).Create(signer),
            (txBuilder with { Nonce = 1L }).Create(signer),
        ];

        blockchain.StagedTransactions.AddRange(txs);

        _ = blockchain.ProposeAndAppend(proposer);

        Transaction[] staleTxs =
        [
            (txBuilder with { Nonce = 0L }).Create(signer),
            (txBuilder with { Nonce = 1L }).Create(signer),
        ];
        blockchain.StagedTransactions.AddRange(staleTxs);

        Assert.Equal(2L, blockchain.GetNextTxNonce(address));

        blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = actions,
        });
        Assert.Equal(3L, blockchain.GetNextTxNonce(address));

        blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = actions,
        });
        Assert.Equal(4L, blockchain.GetNextTxNonce(address));
    }

    [Fact]
    public void ValidateTxNonces()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var addressA = RandomUtility.Address(random);
        var actions = new[] { DumbAction.Create((addressA, string.Empty)) };

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var genesisBlockHash = genesisBlock.BlockHash;
        var blockchain = new Blockchain(genesisBlock);
        var txBuilder = new TransactionBuilder
        {
            GenesisBlockHash = genesisBlockHash,
            Actions = actions,
        };

        Block ProposeNext(Block previousBlock, ImmutableSortedSet<Transaction> txs)
        {
            return new BlockBuilder
            {
                Height = previousBlock.Height + 1,
                PreviousBlockHash = previousBlock.BlockHash,
                PreviousBlockCommit = CreateBlockCommit(previousBlock),
                PreviousStateRootHash = blockchain.GetStateRootHash(previousBlock.BlockHash),
                Timestamp = previousBlock.Timestamp + TimeSpan.FromSeconds(10),
                Transactions = txs,
            }.Create(proposer);
        }

        ImmutableSortedSet<Transaction> txsA =
        [
            (txBuilder with { Nonce = 1L }).Create(signer),
            (txBuilder with { Nonce = 0L }).Create(signer),
        ];
        var block1 = ProposeNext(genesisBlock, txsA);
        blockchain.Append(block1, CreateBlockCommit(block1));

        ImmutableSortedSet<Transaction> txsB =
        [
            (txBuilder with { Nonce = 2L }).Create(signer),
        ];
        var block2 = ProposeNext(block1, txsB);
        blockchain.Append(block2, CreateBlockCommit(block2));

        // Invalid if nonce is too low
        ImmutableSortedSet<Transaction> txsC =
        [
            (txBuilder with { Nonce = 1L }).Create(signer),
        ];
        var block3A = ProposeNext(block2, txsC);
        var e1 = Assert.Throws<ArgumentException>(() => blockchain.Append(block3A, CreateBlockCommit(block3A)));
        Assert.Contains("has an invalid nonce", e1.Message);

        // Invalid if nonce is too high
        ImmutableSortedSet<Transaction> txsD =
        [
            (txBuilder with { Nonce = 4L }).Create(signer),
        ];
        var block3B = ProposeNext(block2, txsD);
        var e2 = Assert.Throws<ArgumentException>(() => blockchain.Append(block3B, CreateBlockCommit(block3B)));
        Assert.Contains("has an invalid nonce", e2.Message);
    }

    [Fact]
    public void MakeTransactionWithSystemAction()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var address = signer.Address;
        var action = new Initialize
        {
            Validators = [new Validator { Address = RandomUtility.Address(random) }],
        };

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [action],
        });
        blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [action],
        });

        var txs = blockchain.StagedTransactions.Values.OrderBy(tx => tx.Nonce).ToArray();

        Assert.Equal(2, txs.Length);

        var transaction = txs[0];
        Assert.Equal(0, transaction.Nonce);
        Assert.Equal(address, transaction.Signer);
        Assert.Equal(action.ToBytecode(), transaction.Actions[0]);

        transaction = txs[1];
        Assert.Equal(1, transaction.Nonce);
        Assert.Equal(address, transaction.Signer);
        Assert.Equal(action.ToBytecode(), transaction.Actions[0]);
    }

    [Fact]
    public void MakeTransactionWithCustomActions()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var address = signer.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = actions,
        });
        blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = actions,
        });

        var txs = blockchain.StagedTransactions.Values.OrderBy(tx => tx.Nonce).ToArray();

        Assert.Equal(2, txs.Length);

        var transaction = txs[0];
        Assert.Equal(0, transaction.Nonce);
        Assert.Equal(address, transaction.Signer);
        Assert.Equal(actions.ToBytecodes(), transaction.Actions);

        transaction = txs[1];
        Assert.Equal(1, transaction.Nonce);
        Assert.Equal(address, transaction.Signer);
        Assert.Equal(actions.ToBytecodes(), transaction.Actions);
    }

    [Fact]
    public async Task MakeTransactionConcurrency()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var address = signer.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => blockchain.StagedTransactions.Add(signer, @params: new()
            {
                Actions = actions,
            })));

        await Task.WhenAll(tasks);

        var txIds = blockchain.StagedTransactions.Keys;

        var nonces = txIds
            .Select(id => blockchain.StagedTransactions[id])
            .Select(tx => tx.Nonce)
            .OrderBy(nonce => nonce)
            .ToArray();

        Assert.Equal(
            nonces,
            [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
    }

    [Fact]
    public void BlockActionWithMultipleAddress()
    {
        var random = RandomUtility.GetRandom(_output);
        var miner0 = RandomUtility.Signer(random);
        var miner1 = RandomUtility.Signer(random);
        var miner2 = RandomUtility.Signer(random);
        var rewardRecordAddress = MinerReward.RewardRecordAddress;

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        _ = blockchain.ProposeAndAppend(miner1);
        _ = blockchain.ProposeAndAppend(miner1);
        _ = blockchain.ProposeAndAppend(miner2);

        var miner1state = (int)blockchain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(miner1.Address);
        var miner2state = (int)blockchain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(miner2.Address);
        var rewardState = (string)blockchain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(rewardRecordAddress);

        Assert.Equal(2, miner1state);
        Assert.Equal(1, miner2state);

        Assert.Equal(
            $"{miner0},{miner1.Address},{miner1.Address},{miner2.Address}",
            rewardState);
    }

    [Fact]
    public async Task TipChanged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        var block = blockchain.Propose(RandomUtility.Signer(random));
        var blockCommit = CreateBlockCommit(block);
        var waitTask = blockchain.TipChanged.WaitAsync(e => e.Height == 1);
        blockchain.Append(block, blockCommit);
        var tip = await waitTask.WaitAsync(cancellationToken);
        Assert.Equal(block, tip);
        Assert.Equal(1, tip.Height);
        Assert.Throws<InvalidOperationException>(() => blockchain.Append(block, CreateBlockCommit(block)));
    }

    [Fact]
    public void CreateWithGenesisBlock()
    {
        var random = RandomUtility.GetRandom(_output);
        var addresses = RandomUtility.Array(random, RandomUtility.Address, 3);
        var validator = new Validator { Address = RandomUtility.Address(random) };
        var proposer = RandomUtility.Signer(random);
        var actions = new IAction[]
        {
            new Initialize
            {
                Validators = [validator],
            },
        };

        var customActions = addresses
            .Select((address, i) => DumbAction.Create((address, i.ToString())))
            .ToArray();

        var systemTxs = actions
            .Select((action, i) => new TransactionMetadata
            {
                Nonce = i,
                Signer = proposer.Address,
                Timestamp = DateTimeOffset.UtcNow,
                Actions = new[] { action }.ToBytecodes(),
            }.Sign(proposer))
            .ToArray();
        var customTxs = new[]
        {
            new TransactionMetadata
            {
                Nonce = systemTxs.Length,
                Signer = proposer.Address,
                Timestamp = DateTimeOffset.UtcNow,
                Actions = customActions.ToBytecodes(),
            }.Sign(proposer),
        };
        var genesisBlock = new BlockBuilder
        {
            Transactions = [.. systemTxs.Concat(customTxs)],
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        var actualValidator = blockchain
            .GetWorld()
            .GetValidators()[0];
        Assert.Equal(validator.Address, actualValidator.Address);
        Assert.Equal(BigInteger.One, actualValidator.Power);

        var states = addresses
            .Select(address => blockchain
                .GetWorld()
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(address))
            .ToArray();
        for (var i = 0; i < states.Length; ++i)
        {
            Assert.Equal(states[i], i.ToString());
        }
    }

    [Fact]
    public void FilterLowerNonceTxAfterStaging()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        var txsA = Enumerable.Range(0, 3)
            .Select(nonce => new TransactionBuilder
            {
                Nonce = nonce,
                GenesisBlockHash = genesisBlock.BlockHash,
            }.Create(signer))
            .ToArray();
        blockchain.StagedTransactions.AddRange(txsA);

        var (block1, _) = blockchain.ProposeAndAppend(signer);
        Assert.Equal(txsA, block1.Transactions);

        var txsB = Enumerable.Range(0, 4)
            .Select(nonce => new TransactionBuilder
            {
                Nonce = nonce,
                GenesisBlockHash = genesisBlock.BlockHash,
            }.Create(signer))
            .ToArray();
        blockchain.StagedTransactions.AddRange(txsB);

        // Stage only txs having higher or equal with nonce than expected nonce.
        Assert.Equal(4, blockchain.StagedTransactions.Count);

        var block2 = blockchain.Propose(signer);
        var tx = Assert.Single(block2.Transactions);
        Assert.Equal(3, tx.Nonce);
    }

    [Fact]
    public void CheckIfTxPolicyExceptionHasInnerException()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = TestUtils.Validators,
        }.Create(proposer);
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(
                        obj => throw new InvalidOperationException("tx always throws")),
                ],
            },
        };
        var blockchain = new Blockchain(genesisBlock, options);

        var signer = RandomUtility.Signer(random);
        _ = blockchain.StagedTransactions.Add(signer, @params: new());
        var block = blockchain.Propose(proposer);
        var blockCommit = CreateBlockCommit(block);

        var e = Assert.Throws<InvalidOperationException>(() => blockchain.Append(block, blockCommit));
        Assert.Equal("tx always throws", e.Message);
    }

    [Fact]
    public void ValidateNextBlockCommitOnValidatorSetChange()
    {
        var random = RandomUtility.GetRandom(_output);
        var signersA = RandomUtility.Array(random, RandomUtility.Signer, 4).OrderBy(signer => signer.Address);
        var newSigner = RandomUtility.Signer(random);
        var signersB = signersA.Concat([newSigner]).OrderBy(signer => signer.Address);
        var validatorsA = signersA.Select(signer => new Validator { Address = signer.Address });
        var validatorsB = signersB.Select(signer => new Validator { Address = signer.Address });
        var newValidator = validatorsB.First(item => item.Address == newSigner.Address);

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = [.. validatorsA],
        }.Create(proposer);

        var blockchain = new Blockchain(genesisBlock);

        blockchain.StagedTransactions.Add(RandomUtility.Signer(random), @params: new()
        {
            Actions = [new SetValidator { Validator = newValidator }],
        });
        var block1 = blockchain.Propose(RandomUtility.Signer(random));
        var blockCommit1 = new BlockCommit
        {
            Height = block1.Height,
            Round = 0,
            BlockHash = block1.BlockHash,
            Votes =
            [
                .. validatorsA.Zip(signersA, (v, s) => new VoteBuilder
                    {
                        Validator = v,
                        Block = block1,
                    }.Create(s))
            ],
        };
        blockchain.Append(block1, blockCommit1);

        blockchain.StagedTransactions.Add(RandomUtility.Signer(random), @params: new()
        {
            Actions = [new SetValidator { Validator = RandomUtility.Validator(random) }],
        });
        var block2 = blockchain.Propose(RandomUtility.Signer(random));
        var blockCommit2 = new BlockCommit
        {
            Height = block2.Height,
            Round = 0,
            BlockHash = block2.BlockHash,
            Votes =
            [
                .. validatorsB.Zip(signersB, (v, s) => new VoteBuilder
                    {
                        Validator = v,
                        Block = block2,
                    }.Create(s))
            ],
        };
        blockchain.Append(block2, blockCommit2);

        blockchain.StagedTransactions.Add(RandomUtility.Signer(random), @params: new()
        {
            Actions = [new SetValidator { Validator = RandomUtility.Validator(random) }],
        });
        var block3 = blockchain.Propose(RandomUtility.Signer(random));
        var blockCommit3 = new BlockCommit
        {
            Height = block3.Height,
            Round = 0,
            BlockHash = block3.BlockHash,
            Votes =
            [
                .. validatorsB.Zip(signersB, (v, s) => new VoteBuilder
                    {
                        Validator = v,
                        Block = block3,
                    }.Create(s))
            ],
        };

        Assert.Throws<ArgumentException>("blockCommit", () => blockchain.Append(block3, blockCommit3));
        Assert.Equal(blockchain.GetWorld(0).GetValidators(), [.. validatorsA]);
        Assert.Equal(blockchain.GetWorld(1).GetValidators(), [.. validatorsB]);
    }

    protected virtual RepositoryFixture GetStoreFixture(BlockchainOptions? options = null)
        => new MemoryRepositoryFixture(options ?? new());
}
