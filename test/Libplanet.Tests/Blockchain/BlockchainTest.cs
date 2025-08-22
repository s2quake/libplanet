using System.Security.Cryptography;
using Libplanet.State;
using Libplanet.State.Builtin;
using Libplanet.State.Tests.Actions;
using Libplanet.Data;
using Libplanet.Tests.Store;
using Libplanet.Types;
using static Libplanet.Tests.TestUtils;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Extensions;
using System.Threading.Tasks;

namespace Libplanet.Tests.Blockchain;

public partial class BlockchainTest : IDisposable
{
    [Obsolete]
    private readonly RepositoryFixture _fx;
    [Obsolete]
    private readonly BlockchainOptions _options;
    [Obsolete]
    private readonly Libplanet.Blockchain _blockchain;
    [Obsolete]
    private readonly Block _validNext;
    [Obsolete]
    private readonly StagedTransactionCollection _stagePolicy;
    private readonly ITestOutputHelper _output;

    public BlockchainTest(ITestOutputHelper output)
    {
        _output = output;
        _options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
            BlockOptions = new BlockOptions
            {
                MaxTransactionsBytes = 50 * 1024,
            },
        };

        _fx = GetStoreFixture(_options);
        _blockchain = new Libplanet.Blockchain(_fx.GenesisBlock, _fx.Repository, _options);
        _stagePolicy = _blockchain.StagedTransactions;

        _validNext = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = BlockHeader.CurrentProtocolVersion,
                Height = 1,
                Timestamp = _fx.GenesisBlock.Timestamp.AddSeconds(1),
                Proposer = _fx.Proposer.Address,
                PreviousHash = _fx.GenesisBlock.BlockHash,
            },
        }.Sign(_fx.Proposer);
    }

    public void Dispose()
    {
        _fx.Dispose();
    }

    [Fact]
    public void BaseTest()
    {
        var blockchain = new Libplanet.Blockchain();
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
        var blockchain = new Libplanet.Blockchain(genesisBlock);
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
        var blockchain = new Libplanet.Blockchain(genesisBlock);
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
        var validators = _blockchain.GetWorld().GetValidators();
        Assert.Equal(TestUtils.Validators, validators);
    }

    [Fact]
    public void CanFindBlockByHeight()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new BlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock);

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
        var blockchain = new Libplanet.Blockchain(genesisBlock);

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

        var blockchain = new Libplanet.Blockchain(genesisBlock);

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
        var block3 = blockchain.ProposeBlock(RandomUtility.Signer(random));
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
        var block = blockchain.ProposeBlock(RandomUtility.Signer(random));
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
            PreviousHash = block1.BlockHash,
            PreviousCommit = blockCommit1,
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
            PreviousHash = block1.BlockHash,
            PreviousCommit = blockCommit1,
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

        // var block1 = _blockchain.ProposeBlock(RandomUtility.Signer(random));
        // var blockCommit1 = CreateBlockCommit(block1);
        // _blockchain.Append(block1, blockCommit1);
        var (block1, blockCommit1) = blockchain.ProposeAndAppend(RandomUtility.Signer(random));

        Assert.Equal(blockCommit1, blockchain.BlockCommits[block1.Height]);
        Assert.Equal(blockCommit1, blockchain.BlockCommits[block1.BlockHash]);

        var block2 = new BlockBuilder
        {
            Height = 2,
            PreviousHash = block1.BlockHash,
            PreviousCommit = CreateBlockCommit(blockchain.Tip),
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [],
        }.Create(RandomUtility.Signer(random));
        // var block2 = blockchain.ProposeBlock(RandomUtility.Signer(random));
        var blockCommit2 = CreateBlockCommit(block2);
        blockchain.Append(block2, blockCommit2);
        // var (block2, _) = blockchain.ProposeAndAppend(RandomUtility.Signer(random));

        Assert.NotEqual(blockCommit1, blockchain.BlockCommits[block1.Height]);
        Assert.Equal(block2.PreviousCommit, blockchain.BlockCommits[block1.Height]);
        Assert.Equal(block2.PreviousCommit, blockchain.BlockCommits[block1.BlockHash]);
    }

    // [Fact]
    // public void CleanupBlockCommitStore()
    // {
    //     BlockCommit blockCommit1 = CreateBlockCommit(
    //         new BlockHash(GetRandomBytes(BlockHash.Size)), 1, 0);
    //     BlockCommit blockCommit2 = CreateBlockCommit(
    //         new BlockHash(GetRandomBytes(BlockHash.Size)), 2, 0);
    //     BlockCommit blockCommit3 = CreateBlockCommit(
    //         new BlockHash(GetRandomBytes(BlockHash.Size)), 3, 0);

    //     _blockchain._repository.BlockCommits.Add(blockCommit1);
    //     _blockchain._repository.BlockCommits.Add(blockCommit2);
    //     _blockchain._repository.BlockCommits.Add(blockCommit3);
    //     _blockchain.CleanupBlockCommitStore(blockCommit3.Height);

    //     Assert.Null(_blockchain._repository.BlockCommits[blockCommit1.BlockHash]);
    //     Assert.Null(_blockchain._repository.BlockCommits[blockCommit2.BlockHash]);
    //     Assert.Equal(blockCommit3, _blockchain._repository.BlockCommits[blockCommit3.BlockHash]);
    // }

    [Fact]
    public void GetStatesOnCreatingBlockChain()
    {
        var random = RandomUtility.GetRandom(_output);
        bool invoked = false;
        var options = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                Validator = new RelayObjectValidator<Block>(obj => invoked = true),
            },
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(obj => invoked = true),
                ],
            },
        };
        var repository = new Repository();
        var txSigner = RandomUtility.Signer(random);
        var genesisRawBlock = ProposeGenesis(
            proposer: GenesisProposer,
            transactions:
            [
                new TransactionMetadata
                {
                    Signer = txSigner.Address,
                    Actions = [],
                }.Sign(txSigner),
            ]);
        Block genesisWithTx = genesisRawBlock.Sign(GenesisProposer);
        var chain = new Libplanet.Blockchain(genesisWithTx, repository, options);
        Assert.False(invoked);
    }

    // This is a regression test for:
    // https://github.com/planetarium/libplanet/issues/189#issuecomment-482443607.
    [Fact]
    public void GetStateOnlyDrillsDownUntilRequestedAddressesAreFound()
    {
        var random = RandomUtility.GetRandom(_output);
        var options = new BlockchainOptions();
        var repository = new Repository();
        var chain = new Libplanet.Blockchain(_fx.GenesisBlock, repository, options);

        Block b = chain.Genesis;
        Address[] addresses = new Address[30];
        for (int i = 0; i < addresses.Length; ++i)
        {
            var signer = RandomUtility.Signer(random);
            Address address = signer.Address;
            addresses[i] = address;
            DumbAction[] actions =
            [
                DumbAction.Create((address, "foo")),
                DumbAction.Create((i < 1 ? address : addresses[i - 1], "bar")),
            ];
            Transaction[] txs =
            [
                new TransactionMetadata
                {
                    Signer = signer.Address,
                    GenesisBlockHash = chain.Genesis.BlockHash,
                    Actions = actions.ToBytecodes(),
                }.Sign(signer),
            ];
            b = chain.ProposeBlock(_fx.Proposer);
            chain.Append(b, CreateBlockCommit(b));
        }

        // tracker.ClearLogs();
        int testingDepth = addresses.Length / 2;
        Address[] targetAddresses = Enumerable.Range(
            testingDepth,
            Math.Min(10, addresses.Length - testingDepth - 1))
        .Select(i => addresses[i]).ToArray();

        Assert.All(
            targetAddresses.Select(
                targetAddress => chain
                    .GetWorld()
                    .GetAccount(SystemAddresses.SystemAccount)
                    .GetValue(targetAddress)),
            Assert.NotNull);

        // var callCount = tracker.Logs.Where(
        //     trackLog => trackLog.Method == "GetBlockStates")
        // .Select(trackLog => trackLog.Params).Count();
        // Assert.True(testingDepth >= callCount);
    }

    [Fact]
    public void GetStateReturnsEarlyForNonexistentAccount()
    {
        var options = new BlockchainOptions();
        var repository = new Repository();
        var chain = new Libplanet.Blockchain(_fx.GenesisBlock, repository, options);
        Block b = chain.Genesis;
        for (int i = 0; i < 20; ++i)
        {
            b = chain.ProposeBlock(_fx.Proposer);
            chain.Append(b, CreateBlockCommit(b));
        }

        // tracker.ClearLogs();
        Address nonexistent = new PrivateKey().Address;
        var result = chain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(nonexistent);
        Assert.Null(result);
        // var callCount = tracker.Logs.Where(
        //     trackLog => trackLog.Method == "GetBlockStates")
        // .Select(trackLog => trackLog.Params).Count();
        // Assert.True(
        //     callCount <= 1,
        //     $"GetBlocksStates() was called {callCount} times");
    }

    [Fact]
    public void GetStateReturnsLatestStatesWhenMultipleAddresses()
    {
        var random = RandomUtility.GetRandom(_output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 10);
        var addresses = signers.Select(signer => signer.Address).ToList();
        var options = new BlockchainOptions();
        var repository = new Repository();
        var chain = new Libplanet.Blockchain(_fx.GenesisBlock, repository, options);

        Assert.All(
            addresses.Select(
                address => chain
                    .GetWorld()
                    .GetAccount(SystemAddresses.SystemAccount)
                    .GetValue(address)),
            Assert.Null);
        foreach (var address in addresses)
        {
            Assert.Null(chain
                .GetWorld()
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(address));
        }

        var privateKeysAndAddresses10 = signers.Zip(addresses, (k, a) => (k, a));
        foreach (var (key, address) in privateKeysAndAddresses10)
        {
            chain.StagedTransactions.Add(key, @params: new()
            {
                Actions = [DumbAction.Create((address, "1"))],
            });
        }

        Block block1 = chain.ProposeBlock(signers[0]);

        chain.Append(block1, CreateBlockCommit(block1));

        Assert.All(
            addresses.Select(
                address => chain
                    .GetWorld()
                    .GetAccount(SystemAddresses.SystemAccount)
                    .GetValue(address)),
            v => Assert.Equal("1", v));
        foreach (var address in addresses)
        {
            Assert.Equal(
                "1",
                chain
                    .GetWorld()
                    .GetAccount(SystemAddresses.SystemAccount)
                    .GetValue(address));
        }

        chain.StagedTransactions.Add(signers[0], @params: new()
        {
            Actions = new[] { DumbAction.Create((addresses[0], "2")) },
        });
        Block block2 = chain.ProposeBlock(signers[0]);
        chain.Append(block2, CreateBlockCommit(block2));
        Assert.Equal(
            "1,2",
            chain
                .GetWorld()
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(addresses[0]));
        Assert.All(
            addresses.Skip(1).Select(
                address => chain
                    .GetWorld()
                    .GetAccount(SystemAddresses.SystemAccount)
                    .GetValue(address)),
            v => Assert.Equal("1", v));
    }

    [Fact]
    public void FindBranchPoint()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        Block b1 = _blockchain.ProposeBlock(signer);
        _blockchain.Append(b1, CreateBlockCommit(b1));
        Block b2 = _blockchain.ProposeBlock(signer);
        _blockchain.Append(b2, CreateBlockCommit(b2));
        Block b3 = _blockchain.ProposeBlock(signer);
        _blockchain.Append(b3, CreateBlockCommit(b3));
        Block b4 = _blockchain.ProposeBlock(signer);
        _blockchain.Append(b4, CreateBlockCommit(b4));

        Assert.Equal(b1.PreviousHash, _blockchain.Genesis.BlockHash);

        var emptyLocator = _blockchain.Genesis.BlockHash;
        var invalidLocator =
            new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        var locator = b4.BlockHash;

        using var emptyFx = new MemoryRepositoryFixture(_options);
        using var forkFx = new MemoryRepositoryFixture(_options);

        var emptyRepository = new Repository();
        var emptyChain = new Libplanet.Blockchain(emptyFx.GenesisBlock, emptyRepository, _options);
        var forkRepository = new Repository();
        var forkChain = new Libplanet.Blockchain(forkFx.GenesisBlock, forkRepository, _options);
        forkChain.Append(b1, CreateBlockCommit(b1));
        forkChain.Append(b2, CreateBlockCommit(b2));
        Block b5 = forkChain.ProposeBlock(signer);
        forkChain.Append(b5, CreateBlockCommit(b5));

        // Testing emptyChain
        Assert.Contains(emptyLocator, emptyChain.Blocks.Keys);
        Assert.DoesNotContain(invalidLocator, emptyChain.Blocks.Keys);
        Assert.DoesNotContain(locator, emptyChain.Blocks.Keys);

        // Testing _blockchain
        Assert.Contains(emptyLocator, _blockchain.Blocks.Keys);
        Assert.DoesNotContain(invalidLocator, _blockchain.Blocks.Keys);
        Assert.Contains(locator, _blockchain.Blocks.Keys);

        // Testing fork
        Assert.Contains(emptyLocator, forkChain.Blocks.Keys);
        Assert.DoesNotContain(invalidLocator, forkChain.Blocks.Keys);
        Assert.DoesNotContain(locator, forkChain.Blocks.Keys);
    }

    [Fact]
    public void GetNextTxNonce()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var address = signer.Address;
        var actions = new[] { DumbAction.Create((_fx.Address1, "foo")) };

        Assert.Equal(0, _blockchain.GetNextTxNonce(address));

        Transaction[] txsA =
        [
            _fx.MakeTransaction(actions, signer: signer, nonce: 0),
        ];

        _blockchain.StagedTransactions.AddRange(txsA);
        var block = _blockchain.ProposeBlock(_fx.Proposer);
        var blockCommit = CreateBlockCommit(block);
        _blockchain.Append(block, blockCommit);

        Assert.Equal(1, _blockchain.GetNextTxNonce(address));

        Transaction[] txsB =
        [
            _fx.MakeTransaction(actions, signer: signer, nonce: 1),
            _fx.MakeTransaction(actions, signer: signer, nonce: 2),
        ];

        _blockchain.StagedTransactions.AddRange(txsB);

        Assert.Equal(3, _blockchain.GetNextTxNonce(address));

        Transaction[] txsC =
        [
            _fx.MakeTransaction(actions, signer: signer, nonce: 3),
            _fx.MakeTransaction(actions, signer: signer, nonce: 3),
        ];
        _blockchain.StagedTransactions.AddRange(txsC);

        Assert.Equal(4, _blockchain.GetNextTxNonce(address));

        Transaction[] txsD =
        [
            _fx.MakeTransaction(actions, signer: signer, nonce: 5),
        ];
        _blockchain.StagedTransactions.AddRange(txsD);

        Assert.Equal(4, _blockchain.GetNextTxNonce(address));

        Transaction[] txsE =
        [
            _fx.MakeTransaction(actions, signer: signer, nonce: 4),
            _fx.MakeTransaction(actions, signer: signer, nonce: 5),
            _fx.MakeTransaction(actions, signer: signer, nonce: 7),
        ];
        _blockchain.StagedTransactions.AddRange(txsE);

        foreach (var tx in _blockchain.StagedTransactions.Values)
        {
            // _logger.Fatal(
            //     "{Id}; {Signer}; {Nonce}; {Timestamp}",
            //     tx.Id,
            //     tx.Signer,
            //     tx.Nonce,
            //     tx.Timestamp);
        }

        Assert.Equal(6, _blockchain.GetNextTxNonce(address));
    }

    [Fact]
    public void GetNextTxNonceWithStaleTx()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var address = signer.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        Transaction[] txs =
        [
            _fx.MakeTransaction(actions, signer: signer),
            _fx.MakeTransaction(actions, signer: signer, nonce: 1),
        ];

        _blockchain.StagedTransactions.AddRange(txs);
        Block block = _blockchain.ProposeBlock(signer);
        _blockchain.Append(block, CreateBlockCommit(block));

        Transaction[] staleTxs =
        [
            _fx.MakeTransaction(actions, signer: signer, nonce: 0),
            _fx.MakeTransaction(actions, signer: signer, nonce: 1),
        ];
        _blockchain.StagedTransactions.AddRange(staleTxs);

        Assert.Equal(2, _blockchain.GetNextTxNonce(address));

        _blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = actions,
        });
        Assert.Equal(3, _blockchain.GetNextTxNonce(address));

        _blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = actions,
        });
        Assert.Equal(4, _blockchain.GetNextTxNonce(address));
    }

    [Fact]
    public void ValidateTxNonces()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var actions = new[] { DumbAction.Create((_fx.Address1, string.Empty)) };

        var genesis = _blockchain.Genesis;

        Block ProposeNext(Block block, ImmutableSortedSet<Transaction> txs)
        {
            return TestUtils.ProposeNext(
                block,
                _blockchain.GetStateRootHash(block.BlockHash),
                transactions: txs,
                blockInterval: TimeSpan.FromSeconds(10),
                proposer: _fx.Proposer,
                previousCommit: CreateBlockCommit(block)).Sign(_fx.Proposer);
        }

        ImmutableSortedSet<Transaction> txsA =
        [
            _fx.MakeTransaction(actions, signer: signer, nonce: 1),
            _fx.MakeTransaction(actions, signer: signer, nonce: 0),
        ];
        Block b1 = ProposeNext(genesis, txsA);
        _blockchain.Append(b1, CreateBlockCommit(b1));

        ImmutableSortedSet<Transaction> txsB =
        [
            _fx.MakeTransaction(actions, signer: signer, nonce: 2),
        ];
        Block b2 = ProposeNext(b1, txsB);
        _blockchain.Append(b2, CreateBlockCommit(b2));

        // Invalid if nonce is too low
        ImmutableSortedSet<Transaction> txsC =
        [
            _fx.MakeTransaction(actions, signer: signer, nonce: 1),
        ];
        Block b3a = ProposeNext(b2, txsC);
        Assert.Throws<InvalidOperationException>(() =>
            _blockchain.Append(b3a, CreateBlockCommit(b3a)));

        // Invalid if nonce is too high
        ImmutableSortedSet<Transaction> txsD =
        [
            _fx.MakeTransaction(actions, signer: signer, nonce: 4),
        ];
        Block b3b = ProposeNext(b2, txsD);
        Assert.Throws<InvalidOperationException>(() =>
            _blockchain.Append(b3b, CreateBlockCommit(b3b)));
    }

    [Fact]
    public void MakeTransactionWithSystemAction()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        Address address = signer.Address;
        var action = new Initialize
        {
            Validators = [new Validator { Address = new PrivateKey().Address }],
        };

        _blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [action],
        });
        _blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [action],
        });

        List<Transaction> txs = _stagePolicy
            .Values
            .OrderBy(tx => tx.Nonce)
            .ToList();

        Assert.Equal(2, txs.Count);

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
        Address address = signer.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        _blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = actions,
        });
        _blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = actions,
        });

        List<Transaction> txs = _stagePolicy
            .Values
            .OrderBy(tx => tx.Nonce)
            .ToList();

        Assert.Equal(2, txs.Count);

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

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => _blockchain.StagedTransactions.Add(signer, @params: new()
            {
                Actions = actions,
            })));

        await Task.WhenAll(tasks);

        var txIds = _blockchain.StagedTransactions.Keys;

        var nonces = txIds
            .Select(id => _stagePolicy.GetValueOrDefault(id, _blockchain.Transactions[id]))
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
        var miner0 = _blockchain.Genesis.Proposer;
        var miner1 = RandomUtility.Signer(random);
        var miner2 = RandomUtility.Signer(random);
        var rewardRecordAddress = MinerReward.RewardRecordAddress;

        Block block1 = _blockchain.ProposeBlock(miner1);
        _blockchain.Append(block1, CreateBlockCommit(block1));
        Block block2 = _blockchain.ProposeBlock(miner1);
        _blockchain.Append(block2, CreateBlockCommit(block2));
        Block block3 = _blockchain.ProposeBlock(miner2);
        _blockchain.Append(block3, CreateBlockCommit(block3));

        var miner1state = (int)_blockchain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(miner1.Address);
        var miner2state = (int)_blockchain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(miner2.Address);
        var rewardState = (string)_blockchain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(rewardRecordAddress);

        Assert.Equal(2, miner1state);
        Assert.Equal(1, miner2state);

        Assert.Equal(
            $"{miner0},{miner1.Address},{miner1.Address},{miner2.Address}",
            rewardState);
    }

    /// Configures the store fixture that every test in this class depends on.
    /// Subclasses should override this.
    /// </summary>
    /// <param name="policyActions">The policy block actions to use.</param>
    /// <returns>The store fixture that every test in this class depends on.</returns>
    protected virtual RepositoryFixture GetStoreFixture(BlockchainOptions? options = null)
        => new MemoryRepositoryFixture(options ?? new());

    private (Address[], Transaction[]) MakeFixturesForAppendTests(
        ISigner? signer = null,
        DateTimeOffset? epoch = default,
        PrivateKey[]? keys = null)
    {
        Address[] addresses = keys is PrivateKey[] ks
            ? ks.Select(k => k.Address).ToArray()
            :
            [
                _fx.Address1,
                _fx.Address2,
                _fx.Address3,
                _fx.Address4,
                _fx.Address5,
            ];

        if (addresses.Length != 5)
        {
            throw new ArgumentException("The number of keys must 5.", nameof(keys));
        }

        signer = signer ?? new PrivateKey(
        [
            0xa8, 0x21, 0xc7, 0xc2, 0x08, 0xa9, 0x1e, 0x53, 0xbb, 0xb2,
            0x71, 0x15, 0xf4, 0x23, 0x5d, 0x82, 0x33, 0x44, 0xd1, 0x16,
            0x82, 0x04, 0x13, 0xb6, 0x30, 0xe7, 0x96, 0x4f, 0x22, 0xe0,
            0xec, 0xe0,
        ]).AsSigner();

        Transaction[] txs =
        [
            _fx.MakeTransaction(
                new[]
                {
                    DumbAction.Create((addresses[0], "foo"), (null, addresses[0], 100)),
                    DumbAction.Create((addresses[1], "bar"), (null, addresses[1], 100)),
                },
                timestamp: epoch,
                nonce: 0,
                signer: signer),
            _fx.MakeTransaction(
                new[]
                {
                    DumbAction.Create((addresses[2], "baz"), (null, addresses[2], 100)),
                    DumbAction.Create((addresses[3], "qux"), (null, addresses[3], 100)),
                },
                timestamp: (epoch??DateTimeOffset.UtcNow).AddSeconds(5),
                nonce: 1,
                signer: signer),
        ];

        return (addresses, txs);
    }

    [Fact]
    public void TipChanged()
    {
        var random = RandomUtility.GetRandom(_output);
        var genesisBlock = _blockchain.Genesis;
        TipChangedInfo? tipChangedInfo = null;
        var block = _blockchain.ProposeBlock(RandomUtility.Signer(random));
        var blockCommit = CreateBlockCommit(block);
        using var subscription = _blockchain.TipChanged.Subscribe(i => tipChangedInfo = i);
        _blockchain.Append(block, CreateBlockCommit(block));
        Assert.NotNull(tipChangedInfo);
        Assert.Equal(block, tipChangedInfo.Tip);
        Assert.Equal(1, tipChangedInfo.Tip.Height);
        Assert.Throws<InvalidOperationException>(() => _blockchain.Append(block, blockCommit));
    }

    [Fact]
    public void CreateWithGenesisBlock()
    {
        var random = RandomUtility.GetRandom(_output);
        using var fx = new MemoryRepositoryFixture(new());
        var addresses = ImmutableArray.Create(
            fx.Address1,
            fx.Address2,
            fx.Address3);

        var validatorKey = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var actions = new IAction[]
        {
            new Initialize
            {
                Validators =[new Validator { Address = validatorKey.Address }],
            },
        };

        var customActions =
            addresses
                .Select((address, index) => DumbAction.Create((address, index.ToString())))
                .ToArray();

        var systemTxs = actions
            .Select((systemAction, i) => new TransactionMetadata
            {
                Nonce = i,
                Signer = proposer.Address,
                GenesisBlockHash = default,
                Actions = new[] { systemAction }.ToBytecodes(),
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
                MaxGasPrice = default,
            }.Sign(proposer),
        };
        var genesisBlock = new BlockBuilder
        {
            Transactions = [.. systemTxs.Concat(customTxs)],
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock, fx.Repository, fx.Options);

        var validator = blockchain
            .GetWorld()
            .GetValidators()[0];
        Assert.Equal(validatorKey.Address, validator.Address);
        Assert.Equal(BigInteger.One, validator.Power);

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
    public void ConstructWithUnexpectedGenesisBlock()
    {
        var options = new BlockchainOptions();
        var repository = new Repository();
        Assert.Throws<InvalidOperationException>(() => new Libplanet.Blockchain(repository, options));
    }

    [Fact]
    private void FilterLowerNonceTxAfterStaging()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var txsA = Enumerable.Range(0, 3)
            .Select(nonce => _fx.MakeTransaction(
                nonce: nonce, signer: signer, timestamp: DateTimeOffset.Now))
            .ToArray();
        _blockchain.StagedTransactions.AddRange(txsA);
        Block b1 = _blockchain.ProposeBlock(signer);
        _blockchain.Append(b1, CreateBlockCommit(b1));
        Assert.Equal(txsA, b1.Transactions);

        var txsB = Enumerable.Range(0, 4)
            .Select(nonce => _fx.MakeTransaction(
                nonce: nonce, signer: signer, timestamp: DateTimeOffset.Now))
            .ToArray();
        _blockchain.StagedTransactions.AddRange(txsB);

        // Stage only txs having higher or equal with nonce than expected nonce.
        Assert.Single(_blockchain.StagedTransactions.Keys);
        // Assert.Single(_blockchain.StagedTransactions.Iterate(filtered: true));
        Assert.Equal(4, _blockchain.StagedTransactions.Count);
    }

    [Fact]
    private void CheckIfTxPolicyExceptionHasInnerException()
    {
        var random = RandomUtility.GetRandom(_output);
        // var policy = new NullPolicyButTxPolicyAlwaysThrows(
        //     x =>
        //     {
        //         // ReSharper disable AccessToModifiedClosure
        //         // The following method calls should not throw any exceptions:
        //         x?.GetWorld()
        //             .GetAccount(ReservedAddresses.LegacyAccount)
        //             .GetValue(default);
        //         x?.GetWorld()
        //             .GetAccount(ReservedAddresses.LegacyAccount)
        //             .GetValue(default);
        //         // ReSharper restore AccessToModifiedClosure
        //     });
        var options = new BlockchainOptions();
        var repository = new Repository();
        var genesisTxSigner = RandomUtility.Signer(random);
        var genesisTx = new TransactionMetadata
        {
            Signer = genesisTxSigner.Address,
            Actions = [],
        }.Sign(genesisTxSigner);
        var genesisWithTx = ProposeGenesis(GenesisProposer, transactions: [genesisTx]).Sign(GenesisProposer);

        var chain = new Libplanet.Blockchain(genesisWithTx, repository, options);

        var blockTxSigner = RandomUtility.Signer(random);
        var blockTx = new TransactionMetadata
        {
            Signer = blockTxSigner.Address,
            Actions = Array.Empty<DumbAction>().ToBytecodes(),
        }.Sign(blockTxSigner);
        var nextStateRootHash = chain.GetStateRootHash(genesisWithTx.BlockHash);
        var block = ProposeNextBlock(
            previousBlock: chain.Genesis,
            proposer: GenesisProposer,
            txs: [blockTx],
            previousStateRootHash: (HashDigest<SHA256>)nextStateRootHash);

        var e = Assert.Throws<InvalidOperationException>(
            () => chain.Append(block, CreateBlockCommit(block)));
        Assert.NotNull(e.InnerException);
    }

    [Fact]
    private void ValidateNextBlockCommitOnValidatorSetChange()
    {
        var random = RandomUtility.GetRandom(_output);
        var storeFixture = new MemoryRepositoryFixture();
        var options = storeFixture.Options;
        var repository = storeFixture.Repository;
        var addresses = ImmutableList<Address>.Empty
            .Add(storeFixture.Address1)
            .Add(storeFixture.Address2)
            .Add(storeFixture.Address3);

        var newValidator = RandomUtility.Signer(random);
        var newValidators = Signers.Append(newValidator).ToArray();
        var newValidatorPowers = TestUtils.Validators.Select(v => v.Power)
            .Append(BigInteger.One).ToArray();
        var initialValidatorSet =
            Signers.Select(
                pk => new Validator { Address = pk.Address })
            .ToImmutableSortedSet();
        var systemActions = new[]
        {
            new Initialize
            {
                Validators = initialValidatorSet,
            },
        };
        var signer = RandomUtility.Signer(random);
        var txs = systemActions
            .Select((systemAction, i) => new TransactionMetadata
            {
                Nonce = i,
                Signer = signer.Address,
                GenesisBlockHash = default,
                Actions = new IAction[] { systemAction }.ToBytecodes(),
            }.Sign(signer))
            .ToImmutableList();
        var genesisBlock = new BlockBuilder
        {
            Transactions = [.. txs],
        }.Create(signer);

        var blockchain = new Libplanet.Blockchain(genesisBlock, repository, options);

        blockchain.StagedTransactions.Add(RandomUtility.Signer(random), @params: new()
        {
            Actions =
            [
                new SetValidator
                {
                    Validator = new Validator { Address = newValidator.Address },
                },
            ],
        });
        var newBlock = blockchain.ProposeBlock(RandomUtility.Signer(random));
        var newBlockCommit = new BlockCommit
        {
            Height = newBlock.Height,
            Round = 0,
            BlockHash = newBlock.BlockHash,
            Votes = [.. Signers.Select(
                pk => new VoteMetadata
                {
                    Height = newBlock.Height,
                    Round = 0,
                    BlockHash = newBlock.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = pk.Address,
                    ValidatorPower = TestUtils.Validators.GetValidator(pk.Address).Power,
                    Type = VoteType.PreCommit,
                }.Sign(pk))
            .OrderBy(vote => vote.Validator)],
        };
        blockchain.Append(newBlock, newBlockCommit);

        blockchain.StagedTransactions.Add(RandomUtility.Signer(random), @params: new()
        {
            Actions =
            [
                new SetValidator
                {
                    Validator = new Validator { Address = new PrivateKey().Address },
                },
            ],
        });
        var nextBlock = blockchain.ProposeBlock(RandomUtility.Signer(random));
        var nextBlockCommit = new BlockCommit
        {
            Height = nextBlock.Height,
            Round = 0,
            BlockHash = nextBlock.BlockHash,
            Votes = [.. Enumerable.Range(0, newValidators.Length)
                .Select(
                    index => new VoteMetadata
                    {
                        Height = nextBlock.Height,
                        Round = 0,
                        BlockHash = nextBlock.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = newValidators[index].Address,
                        ValidatorPower = newValidatorPowers[index],
                        Type = VoteType.PreCommit,
                    }.Sign(newValidators[index]))
                .OrderBy(vote => vote.Validator)],
        };
        blockchain.Append(nextBlock, nextBlockCommit);

        blockchain.StagedTransactions.Add(RandomUtility.Signer(random), @params: new()
        {
            Actions = new[]
            {
                new SetValidator
                {
                    Validator = new Validator { Address = new PrivateKey().Address },
                },
            },
        });
        var invalidCommitBlock = blockchain.ProposeBlock(RandomUtility.Signer(random));

        Assert.Throws<InvalidOperationException>(
            () => blockchain.Append(
                invalidCommitBlock,
                new BlockCommit
                {
                    Height = invalidCommitBlock.Height,
                    Round = 0,
                    BlockHash = invalidCommitBlock.BlockHash,
                    Votes = [.. Enumerable.Range(0, newValidators.Length)
                        .Select(
                            index => new VoteMetadata
                            {
                                Height = invalidCommitBlock.Height,
                                Round = 0,
                                BlockHash = invalidCommitBlock.BlockHash,
                                Timestamp = DateTimeOffset.UtcNow,
                                Validator = newValidators[index].Address,
                                ValidatorPower = newValidatorPowers[index],
                                Type = VoteType.PreCommit,
                            }.Sign(newValidators[index]))],
                }));

        Assert.Equal(
            blockchain
                .GetWorld(0)
                .GetValidators(),
            [.. Signers.Select(pk => new Validator { Address = pk.Address })]);

        Assert.Equal(
            blockchain
                .GetWorld(1)
                .GetValidators(),
            [.. newValidators.Select(pk => new Validator { Address = pk.Address })]);
    }

    // private class
    //     NullPolicyButTxPolicyAlwaysThrows : NullPolicyForGetStatesOnCreatingBlockChain
    // {
    //     public NullPolicyButTxPolicyAlwaysThrows(
    //         Action<BlockChain> hook)
    //         : base(hook)
    //     {
    //     }

    //     public override InvalidOperationException ValidateNextBlockTx(
    //         BlockChain blockchain,
    //         Transaction transaction)
    //     {
    //         _hook(blockchain);
    //         return new InvalidOperationException("Test Message");
    //     }
    // }

    // private class NullPolicyForGetStatesOnCreatingBlockChain : NullBlockPolicy
    // {
    //     protected readonly Action<BlockChain> _hook;

    //     public NullPolicyForGetStatesOnCreatingBlockChain(
    //         Action<BlockChain> hook)
    //     {
    //         _hook = hook;
    //     }

    //     public override InvalidOperationException ValidateNextBlockTx(
    //         BlockChain blockchain,
    //         Transaction transaction)
    //     {
    //         _hook(blockchain);
    //         return base.ValidateNextBlockTx(blockchain, transaction);
    //     }

    //     public override Exception ValidateNextBlock(
    //         BlockChain blocks,
    //         Block nextBlock)
    //     {
    //         _hook(blocks);
    //         return base.ValidateNextBlock(blocks, nextBlock);
    //     }
    // }
}
