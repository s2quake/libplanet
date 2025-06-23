using System.Security.Cryptography;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Builtin;
using Libplanet.State.Tests.Actions;
using Libplanet.Data;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Serilog;
using Xunit.Abstractions;
using static Libplanet.Tests.TestUtils;
using Libplanet.Types.Tests;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;

namespace Libplanet.Tests.Blockchain;

public partial class BlockChainTest : IDisposable
{
    private readonly ILogger _logger;
    private readonly RepositoryFixture _fx;
    private readonly BlockchainOptions _options;
    private Libplanet.Blockchain _blockChain;
    private readonly Block _validNext;
    private readonly StagedTransactionCollection _stagePolicy;

    public BlockChainTest(ITestOutputHelper output)
    {
        Log.Logger = _logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithThreadId()
            .WriteTo.TestOutput(output)
            .CreateLogger()
            .ForContext<BlockChainTest>();

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
        _blockChain = new Libplanet.Blockchain(_fx.GenesisBlock, _fx.Repository, _options);
        _stagePolicy = _blockChain.StagedTransactions;

        _validNext = new RawBlock
        {
            Header = new BlockHeader
            {
                Version = BlockHeader.CurrentProtocolVersion,
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
        var blockChain = new Libplanet.Blockchain();
        Assert.NotEqual(Guid.Empty, blockChain.Id);
        Assert.Empty(blockChain.Blocks);
        Assert.Empty(blockChain.BlockCommits);
        Assert.Empty(blockChain.StagedTransactions);
        Assert.Empty(blockChain.Transactions);
        Assert.Empty(blockChain.PendingEvidences);
        Assert.Empty(blockChain.Evidences);
        Assert.Empty(blockChain.TxExecutions);
        Assert.Throws<InvalidOperationException>(() => blockChain.StateRootHash);
        Assert.Throws<InvalidOperationException>(() => blockChain.BlockCommit);
        Assert.Throws<InvalidOperationException>(() => blockChain.Genesis);
        Assert.Throws<InvalidOperationException>(() => blockChain.Tip);
    }

    [Fact]
    public void BaseTest_WithGenesis()
    {
        var proposer = new PrivateKey();
        var genesisBlock = new BlockBuilder
        {
        }.Create(proposer);
        var blockChain = new Libplanet.Blockchain(genesisBlock);
        Assert.NotEqual(Guid.Empty, blockChain.Id);
        Assert.Single(blockChain.Blocks);
        Assert.Single(blockChain.BlockCommits);
        Assert.Empty(blockChain.StagedTransactions);
        Assert.Empty(blockChain.Transactions);
        Assert.Empty(blockChain.PendingEvidences);
        Assert.Empty(blockChain.Evidences);
        Assert.Empty(blockChain.TxExecutions);
        Assert.Equal(default, blockChain.StateRootHash);
        Assert.Equal(BlockCommit.Empty, blockChain.BlockCommit);
        Assert.Equal(genesisBlock, blockChain.Genesis);
        Assert.Equal(genesisBlock, blockChain.Tip);
    }

    [Fact]
    public void BaseTest_WithGenesis_WithTransaction()
    {
        var proposer = new PrivateKey();
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
        var blockChain = new Libplanet.Blockchain(genesisBlock);
        Assert.NotEqual(Guid.Empty, blockChain.Id);
        Assert.Single(blockChain.Blocks);
        Assert.Single(blockChain.BlockCommits);
        Assert.Empty(blockChain.StagedTransactions);
        Assert.Single(blockChain.Transactions);
        Assert.Empty(blockChain.PendingEvidences);
        Assert.Empty(blockChain.Evidences);
        Assert.Single(blockChain.TxExecutions);
        Assert.NotEqual(default, blockChain.StateRootHash);
        Assert.Equal(BlockCommit.Empty, blockChain.BlockCommit);
        Assert.Equal(genesisBlock, blockChain.Genesis);
        Assert.Equal(genesisBlock, blockChain.Tip);
    }

    // [Fact]
    // public void CanonicalId()
    // {
    //     var chain1 = _blockChain;
    //     var key = new PrivateKey();
    //     Block block1 = chain1.ProposeBlock(key);
    //     chain1.Append(block1, CreateBlockCommit(block1));
    //     Block block2 = chain1.ProposeBlock(key);
    //     chain1.Append(block2, CreateBlockCommit(block2));
    //     Assert.Equal(chain1.Id, _fx.Repository.ChainId);
    //     Assert.Equal(chain1.Id, _fx.Repository.ChainId);

    //     var beginActions = ImmutableArray.Create<IAction>();
    //     var endActions = ImmutableArray.Create<IAction>(
    //         new MinerReward(1));

    //     var policy = new BlockChainOptions
    //     {
    //         PolicyActions = new PolicyActions
    //         {
    //             BeginBlockActions = beginActions,
    //             EndBlockActions = endActions,
    //         },
    //     };
    //     var z = new BlockChain(_fx.GenesisBlock, policy);

    //     Assert.Equal(chain1.Id, z.Id);
    // }

    [Fact]
    public void Validators()
    {
        var validatorSet = _blockChain.GetWorld().GetValidators();
        Assert.Equal(TestUtils.Validators, validatorSet);
    }

    [Fact]
    public void CanFindBlockByIndex()
    {
        var proposer = new PrivateKey();
        var genesis = _blockChain.Genesis;
        Assert.Equal(genesis, _blockChain.Blocks[0]);

        var block = _blockChain.ProposeBlock(proposer);
        var blockCommit = CreateBlockCommit(block);
        _blockChain.Append(block, blockCommit);
        Assert.Equal(block, _blockChain.Blocks[1]);
    }

    [Fact]
    public void BlockHashes()
    {
        var proposer = new PrivateKey();
        var genesisBlock = _blockChain.Genesis;

        Assert.Single(_blockChain.Blocks.Keys);

        var block1 = _blockChain.ProposeBlock(proposer);
        _blockChain.Append(block1, CreateBlockCommit(block1));
        Assert.Equal([genesisBlock.BlockHash, block1.BlockHash], _blockChain.Blocks.Keys);

        Block b2 = _blockChain.ProposeBlock(proposer);
        _blockChain.Append(b2, CreateBlockCommit(b2));
        Assert.Equal(
            new[] { genesisBlock.BlockHash, block1.BlockHash, b2.BlockHash },
            _blockChain.Blocks.Keys);

        Block b3 = _blockChain.ProposeBlock(proposer);
        _blockChain.Append(b3, CreateBlockCommit(b3));
        Assert.Equal(
            new[] { genesisBlock.BlockHash, block1.BlockHash, b2.BlockHash, b3.BlockHash },
            _blockChain.Blocks.Keys);
    }

    [Fact]
    public void ProcessActions()
    {
        var actions = new IAction[]
        {
            new Initialize { Validators = TestUtils.Validators, },
        };

        var genesis = new BlockBuilder
        {
            Transactions =
            [
                new TransactionBuilder
                {
                    Actions = actions,
                }.Create(GenesisProposer),
            ],
        }.Create(GenesisProposer);

        var chain = new Libplanet.Blockchain(genesis);
        Block genesisBlock = chain.Genesis;

        IAction[] actions1 =
        [
            new Attack
            {
                Weapon = "sword",
                Target = "goblin",
                TargetAddress = _fx.Address1,
            },
            new Attack
            {
                Weapon = "sword",
                Target = "orc",
                TargetAddress = _fx.Address1,
            },
            new Attack
            {
                Weapon = "staff",
                Target = "goblin",
                TargetAddress = _fx.Address1,
            },
        ];
        var tx1Key = new PrivateKey();
        var tx1 = new TransactionMetadata
        {
            Signer = tx1Key.Address,
            GenesisHash = genesisBlock.BlockHash,
            Actions = actions1.ToBytecodes(),
        }.Sign(tx1Key);

        chain.StagedTransactions.Add(tx1);
        var block1 = chain.ProposeBlock(new PrivateKey());
        var blockCommit1 = CreateBlockCommit(block1);
        chain.Append(block1, blockCommit1);
        var result = (BattleResult)chain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(_fx.Address1);

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
                TargetAddress = _fx.Address1,
            },
        ];
        var tx2Key = new PrivateKey();
        var tx2 = new TransactionMetadata
        {
            Signer = tx2Key.Address,
            GenesisHash = genesisBlock.BlockHash,
            Actions = actions2.ToBytecodes(),
        }.Sign(tx2Key);

        chain.StagedTransactions.Add(tx2);
        Block block2 = chain.ProposeBlock(new PrivateKey());
        chain.Append(block2, CreateBlockCommit(block2));

        result = (BattleResult)chain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(_fx.Address1);

        Assert.Contains("bow", result.UsedWeapons);

        var tx3Key = new PrivateKey();
        var tx3 = new TransactionMetadata
        {
            Signer = tx3Key.Address,
            GenesisHash = genesisBlock.BlockHash,
            Actions = new[]
            {
                new Attack
                {
                    Weapon = "sword",
                    Target = "orc",
                    TargetAddress = _fx.Address1,
                },
            }.ToBytecodes(),
        }.Sign(tx3Key);
        Block block3 = chain.ProposeBlock(new PrivateKey());
        chain.StagedTransactions.Add(tx3);
        chain.Append(block3, CreateBlockCommit(block3));
        result = (BattleResult)chain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(_fx.Address1);
    }

    [Fact]
    public void ActionRenderersHaveDistinctContexts()
    {
        var options = new BlockchainOptions();
        var generatedRandomValueLogs = new List<int>();
        var blockChain = MakeBlockChain(options);
        var privateKey = new PrivateKey();
        blockChain.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((default, string.Empty))],
        });
        var block = blockChain.ProposeBlock(new PrivateKey());
        var blockCommit = CreateBlockCommit(block);

        generatedRandomValueLogs.Clear();
        Assert.Empty(generatedRandomValueLogs);
        // using var subscription1 = blockChain.RenderAction.Subscribe(ActionEvaluated);
        // using var subscription2 = blockChain.RenderAction.Subscribe(ActionEvaluated);
        // blockChain.Append(block, blockCommit);
        // Assert.Equal(2, generatedRandomValueLogs.Count);
        // Assert.Equal(generatedRandomValueLogs[0], generatedRandomValueLogs[1]);

        // void ActionEvaluated(ActionExecutionInfo evaluation)
        //     => generatedRandomValueLogs.Add(evaluation.InputContext.GetRandom().Next());
    }

    [Fact]
    public void RenderActionsAfterBlockIsRendered()
    {
        var options = new BlockchainOptions();
        var blockChain = MakeBlockChain(options);
        var privateKey = new PrivateKey();

        var action = DumbAction.Create((default, string.Empty));
        var actions = new[] { action };
        blockChain.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = actions,
        });
        Block prevBlock = blockChain.Tip;
        Block block = blockChain.ProposeBlock(new PrivateKey());
        blockChain.Append(block, CreateBlockCommit(block));

        Assert.Equal(2, blockChain.Blocks.Count);
        // IReadOnlyList<RenderRecord.BlockEvent> blockLogs = recordingRenderer.BlockRecords;
        // Assert.Equal(2, blockLogs.Count);
        // IReadOnlyList<RenderRecord.ActionBase> actionLogs = recordingRenderer.ActionRecords;
        // Assert.Single(actions);
        // Assert.Equal(prevBlock, blockLogs[0].OldTip);
        // Assert.Equal(block, blockLogs[0].NewTip);
        // Assert.Equal(0, blockLogs[0].Index);
        // Assert.Equal(1, actionLogs[0].Index);
        // Assert.Equal(ModelSerializer.Serialize(action), actionLogs[0].Action);
        // Assert.Equal(prevBlock, blockLogs[1].OldTip);
        // Assert.Equal(block, blockLogs[1].NewTip);
        // Assert.Equal(2, blockLogs[1].Index);
    }

    [Fact]
    public void RenderActionsAfterAppendComplete()
    {
        var policy = new BlockchainOptions();
        var store = new Libplanet.Data.Repository(new MemoryDatabase());
        var stateStore = new StateIndex();

        // IActionRenderer renderer = new AnonymousActionRenderer
        // {
        //     ActionRenderer = (a, __, nextState) =>
        //     {
        //         if (!(a is Dictionary dictionary &&
        //               dictionary.TryGetValue("type_id", out IValue typeId) &&
        //               typeId.Equals((Integer)2)))
        //         {
        //             throw new ThrowException.SomeException("thrown by renderer");
        //         }
        //     },
        // };
        // renderer = new LoggedActionRenderer(renderer, Log.Logger);
        Libplanet.Blockchain blockChain = MakeBlockChain(policy);
        var privateKey = new PrivateKey();

        var action = DumbAction.Create((default, string.Empty));
        var actions = new[] { action };
        blockChain.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = actions,
        });
        Block block = blockChain.ProposeBlock(new PrivateKey());

        ThrowException.SomeException e = Assert.Throws<ThrowException.SomeException>(
            () => blockChain.Append(block, CreateBlockCommit(block)));
        Assert.Equal("thrown by renderer", e.Message);
        Assert.Equal(2, blockChain.Blocks.Count);
    }

    // [Fact]
    // public void FindNextHashes()
    // {
    //     var key = new PrivateKey();
    //     IReadOnlyList<BlockHash> hashes;

    //     hashes = _blockChain.FindNextHashes(_blockChain.Genesis.BlockHash);
    //     Assert.Single(hashes);
    //     Assert.Equal(_blockChain.Genesis.BlockHash, hashes.First());
    //     var block0 = _blockChain.Genesis;
    //     var block1 = _blockChain.ProposeBlock(key);
    //     _blockChain.Append(block1, CreateBlockCommit(block1));
    //     var block2 = _blockChain.ProposeBlock(key);
    //     _blockChain.Append(block2, CreateBlockCommit(block2));
    //     var block3 = _blockChain.ProposeBlock(key);
    //     _blockChain.Append(block3, CreateBlockCommit(block3));

    //     hashes = _blockChain.FindNextHashes(block0.BlockHash);
    //     Assert.Equal(new[] { block0.BlockHash, block1.BlockHash, block2.BlockHash, block3.BlockHash }, hashes);

    //     hashes = _blockChain.FindNextHashes(block1.BlockHash);
    //     Assert.Equal(new[] { block1.BlockHash, block2.BlockHash, block3.BlockHash }, hashes);

    //     hashes = _blockChain.FindNextHashes(block0.BlockHash, count: 2);
    //     Assert.Equal(new[] { block0.BlockHash, block1.BlockHash }, hashes);
    // }

    [Fact]
    public void DetectInvalidTxNonce()
    {
        var privateKey = new PrivateKey();
        var actions = new[] { DumbAction.Create((_fx.Address1, "foo")) };

        var genesis = _blockChain.Genesis;

        Transaction[] txsA =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey),
        ];

        Block b1 = _blockChain.ProposeBlock(_fx.Proposer);
        _blockChain.Append(b1, TestUtils.CreateBlockCommit(b1));

        Block b2 = _blockChain.ProposeBlock(_fx.Proposer);
        Assert.Throws<InvalidOperationException>(() =>
            _blockChain.Append(b2, CreateBlockCommit(b2)));

        Transaction[] txsB =
        [
            _fx.MakeTransaction(
                actions,
                nonce: 1,
                privateKey: privateKey),
        ];
        b2 = _blockChain.ProposeBlock(_fx.Proposer);
        _blockChain.Append(b2, CreateBlockCommit(b2));
    }

    [Fact]
    public void GetBlockLocator()
    {
        var key = new PrivateKey();
        List<Block> blocks = new List<Block>();
        foreach (var i in Enumerable.Range(0, 10))
        {
            var block = _blockChain.ProposeBlock(key);
            _blockChain.Append(block, CreateBlockCommit(block));
            blocks.Add(block);
        }

        var actual = _blockChain.Tip.BlockHash;
        var expected = blocks[9].BlockHash;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetBlockCommit()
    {
        Assert.Equal(BlockCommit.Empty, _blockChain.BlockCommits[0]);
        Assert.Equal(BlockCommit.Empty, _blockChain.BlockCommits[_blockChain.Genesis.BlockHash]);

        var block1 = _blockChain.ProposeBlock(new PrivateKey());
        var blockCommit1 = CreateBlockCommit(block1);
        _blockChain.Append(block1, blockCommit1);
        Assert.Equal(blockCommit1, _blockChain.BlockCommits[block1.Height]);
        Assert.Equal(blockCommit1, _blockChain.BlockCommits[block1.BlockHash]);

        var block2 = _blockChain.ProposeBlock(new PrivateKey());
        var blockCommit2 = CreateBlockCommit(block2);
        _blockChain.Append(block2, blockCommit2);

        Assert.Equal(blockCommit1, _blockChain.BlockCommits[block1.Height]);
        Assert.Equal(block2.PreviousCommit, _blockChain.BlockCommits[block1.Height]);
        Assert.Equal(block2.PreviousCommit, _blockChain.BlockCommits[block1.BlockHash]);
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

    //     _blockChain._repository.BlockCommits.Add(blockCommit1);
    //     _blockChain._repository.BlockCommits.Add(blockCommit2);
    //     _blockChain._repository.BlockCommits.Add(blockCommit3);
    //     _blockChain.CleanupBlockCommitStore(blockCommit3.Height);

    //     Assert.Null(_blockChain._repository.BlockCommits[blockCommit1.BlockHash]);
    //     Assert.Null(_blockChain._repository.BlockCommits[blockCommit2.BlockHash]);
    //     Assert.Equal(blockCommit3, _blockChain._repository.BlockCommits[blockCommit3.BlockHash]);
    // }

    [Fact]
    public void GetStatesOnCreatingBlockChain()
    {
        bool invoked = false;
        var options = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                Validator = new RelayValidator<Block>(obj => invoked = true),
            },
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(obj => invoked = true),
            },
        };
        var repository = new Repository();
        var txKey = new PrivateKey();
        var genesisRawBlock = ProposeGenesis(
            proposer: GenesisProposer,
            transactions:
            [
                new TransactionMetadata
                {
                    Signer = txKey.Address,
                    Actions = [],
                }.Sign(txKey),
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
        var options = new BlockchainOptions();
        var repository = new Repository();
        var chain = new Libplanet.Blockchain(_fx.GenesisBlock, repository, options);

        Block b = chain.Genesis;
        Address[] addresses = new Address[30];
        for (int i = 0; i < addresses.Length; ++i)
        {
            var privateKey = new PrivateKey();
            Address address = privateKey.Address;
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
                    Signer = privateKey.Address,
                    GenesisHash = chain.Genesis.BlockHash,
                    Actions = actions.ToBytecodes(),
                }.Sign(privateKey),
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
        var privateKeys = Enumerable.Range(1, 10).Select(_ => new PrivateKey()).ToList();
        var addresses = privateKeys.Select(key => key.Address).ToList();
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

        var privateKeysAndAddresses10 = privateKeys.Zip(addresses, (k, a) => (k, a));
        foreach (var (key, address) in privateKeysAndAddresses10)
        {
            chain.StagedTransactions.Add(key, submission: new()
            {
                Actions = [DumbAction.Create((address, "1"))],
            });
        }

        Block block1 = chain.ProposeBlock(privateKeys[0]);

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

        chain.StagedTransactions.Add(privateKeys[0], submission: new()
        {
            Actions = new[] { DumbAction.Create((addresses[0], "2")) },
        });
        Block block2 = chain.ProposeBlock(privateKeys[0]);
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
        var key = new PrivateKey();
        Block b1 = _blockChain.ProposeBlock(key);
        _blockChain.Append(b1, CreateBlockCommit(b1));
        Block b2 = _blockChain.ProposeBlock(key);
        _blockChain.Append(b2, CreateBlockCommit(b2));
        Block b3 = _blockChain.ProposeBlock(key);
        _blockChain.Append(b3, CreateBlockCommit(b3));
        Block b4 = _blockChain.ProposeBlock(key);
        _blockChain.Append(b4, CreateBlockCommit(b4));

        Assert.Equal(b1.PreviousHash, _blockChain.Genesis.BlockHash);

        var emptyLocator = _blockChain.Genesis.BlockHash;
        var invalidLocator =
            new BlockHash(RandomUtility.Bytes(BlockHash.Size));
        var locator = b4.BlockHash;

        using var emptyFx = new MemoryRepositoryFixture(_options);
        using var forkFx = new MemoryRepositoryFixture(_options);

        var emptyRepository = new Repository();
        var emptyChain = new Libplanet.Blockchain(emptyFx.GenesisBlock, emptyRepository, _blockChain.Options);
        var forkRepository = new Repository();
        var forkChain = new Libplanet.Blockchain(forkFx.GenesisBlock, forkRepository, _blockChain.Options);
        forkChain.Append(b1, CreateBlockCommit(b1));
        forkChain.Append(b2, CreateBlockCommit(b2));
        Block b5 = forkChain.ProposeBlock(key);
        forkChain.Append(b5, CreateBlockCommit(b5));

        // Testing emptyChain
        Assert.Contains(emptyLocator, emptyChain.Blocks.Keys);
        Assert.DoesNotContain(invalidLocator, emptyChain.Blocks.Keys);
        Assert.DoesNotContain(locator, emptyChain.Blocks.Keys);

        // Testing _blockChain
        Assert.Contains(emptyLocator, _blockChain.Blocks.Keys);
        Assert.DoesNotContain(invalidLocator, _blockChain.Blocks.Keys);
        Assert.Contains(locator, _blockChain.Blocks.Keys);

        // Testing fork
        Assert.Contains(emptyLocator, forkChain.Blocks.Keys);
        Assert.DoesNotContain(invalidLocator, forkChain.Blocks.Keys);
        Assert.DoesNotContain(locator, forkChain.Blocks.Keys);
    }

    [Fact]
    public void GetNextTxNonce()
    {
        var privateKey = new PrivateKey();
        var address = privateKey.Address;
        var actions = new[] { DumbAction.Create((_fx.Address1, "foo")) };

        Assert.Equal(0, _blockChain.GetNextTxNonce(address));

        Transaction[] txsA =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 0),
        ];

        _blockChain.StagedTransactions.AddRange(txsA);
        var block = _blockChain.ProposeBlock(_fx.Proposer);
        var blockCommit = CreateBlockCommit(block);
        _blockChain.Append(block, blockCommit);

        Assert.Equal(1, _blockChain.GetNextTxNonce(address));

        Transaction[] txsB =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 1),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 2),
        ];

        _blockChain.StagedTransactions.AddRange(txsB);

        Assert.Equal(3, _blockChain.GetNextTxNonce(address));

        Transaction[] txsC =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 3),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 3),
        ];
        _blockChain.StagedTransactions.AddRange(txsC);

        Assert.Equal(4, _blockChain.GetNextTxNonce(address));

        Transaction[] txsD =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 5),
        ];
        _blockChain.StagedTransactions.AddRange(txsD);

        Assert.Equal(4, _blockChain.GetNextTxNonce(address));

        Transaction[] txsE =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 4),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 5),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 7),
        ];
        _blockChain.StagedTransactions.AddRange(txsE);

        foreach (var tx in _blockChain.StagedTransactions.Values)
        {
            _logger.Fatal(
                "{Id}; {Signer}; {Nonce}; {Timestamp}",
                tx.Id,
                tx.Signer,
                tx.Nonce,
                tx.Timestamp);
        }

        Assert.Equal(6, _blockChain.GetNextTxNonce(address));
    }

    [Fact]
    public void GetNextTxNonceWithStaleTx()
    {
        var privateKey = new PrivateKey();
        var address = privateKey.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        Transaction[] txs =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 1),
        ];

        _blockChain.StagedTransactions.AddRange(txs);
        Block block = _blockChain.ProposeBlock(privateKey);
        _blockChain.Append(block, CreateBlockCommit(block));

        Transaction[] staleTxs =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 0),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 1),
        ];
        _blockChain.StagedTransactions.AddRange(staleTxs);

        Assert.Equal(2, _blockChain.GetNextTxNonce(address));

        _blockChain.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = actions,
        });
        Assert.Equal(3, _blockChain.GetNextTxNonce(address));

        _blockChain.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = actions,
        });
        Assert.Equal(4, _blockChain.GetNextTxNonce(address));
    }

    [Fact]
    public void ValidateTxNonces()
    {
        var privateKey = new PrivateKey();
        var actions = new[] { DumbAction.Create((_fx.Address1, string.Empty)) };

        var genesis = _blockChain.Genesis;

        Block ProposeNext(Block block, ImmutableSortedSet<Transaction> txs)
        {
            return TestUtils.ProposeNext(
                block,
                _blockChain.GetStateRootHash(block.BlockHash),
                transactions: txs,
                blockInterval: TimeSpan.FromSeconds(10),
                proposer: _fx.Proposer,
                previousCommit: CreateBlockCommit(block)).Sign(_fx.Proposer);
        }

        var txsA = ImmutableSortedSet.Create(
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 1),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 0),
        ]);
        Block b1 = ProposeNext(genesis, txsA);
        _blockChain.Append(b1, CreateBlockCommit(b1));

        var txsB = ImmutableSortedSet.Create(
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 2),
        ]);
        Block b2 = ProposeNext(b1, txsB);
        _blockChain.Append(b2, CreateBlockCommit(b2));

        // Invalid if nonce is too low
        var txsC = ImmutableSortedSet.Create(
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 1),
        ]);
        Block b3a = ProposeNext(b2, txsC);
        Assert.Throws<InvalidOperationException>(() =>
            _blockChain.Append(b3a, CreateBlockCommit(b3a)));

        // Invalid if nonce is too high
        var txsD = ImmutableSortedSet.Create(
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 4),
        ]);
        Block b3b = ProposeNext(b2, txsD);
        Assert.Throws<InvalidOperationException>(() =>
            _blockChain.Append(b3b, CreateBlockCommit(b3b)));
    }

    [Fact]
    public void MakeTransactionWithSystemAction()
    {
        var privateKey = new PrivateKey();
        Address address = privateKey.Address;
        var action = new Initialize
        {
            Validators = [new Validator { Address = new PrivateKey().Address }],
        };

        _blockChain.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [action],
        });
        _blockChain.StagedTransactions.Add(privateKey, submission: new()
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
        var privateKey = new PrivateKey();
        Address address = privateKey.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        _blockChain.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = actions,
        });
        _blockChain.StagedTransactions.Add(privateKey, submission: new()
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
        var privateKey = new PrivateKey();
        var address = privateKey.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => _blockChain.StagedTransactions.Add(privateKey, submission: new()
            {
                Actions = actions,
            })));

        await Task.WhenAll(tasks);

        var txIds = _blockChain.StagedTransactions.Keys;

        var nonces = txIds
            .Select(id => _stagePolicy.GetValueOrDefault(id, _blockChain.Transactions[id]))
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
        var miner0 = _blockChain.Genesis.Proposer;
        var miner1 = new PrivateKey();
        var miner2 = new PrivateKey();
        var rewardRecordAddress = MinerReward.RewardRecordAddress;

        Block block1 = _blockChain.ProposeBlock(miner1);
        _blockChain.Append(block1, CreateBlockCommit(block1));
        Block block2 = _blockChain.ProposeBlock(miner1);
        _blockChain.Append(block2, CreateBlockCommit(block2));
        Block block3 = _blockChain.ProposeBlock(miner2);
        _blockChain.Append(block3, CreateBlockCommit(block3));

        var miner1state = (int)_blockChain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(miner1.Address);
        var miner2state = (int)_blockChain
            .GetWorld()
            .GetAccount(SystemAddresses.SystemAccount)
            .GetValue(miner2.Address);
        var rewardState = (string)_blockChain
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
        PrivateKey privateKey = null,
        DateTimeOffset epoch = default,
        PrivateKey[] keys = null)
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

        privateKey = privateKey ?? new PrivateKey(
        [
            0xa8, 0x21, 0xc7, 0xc2, 0x08, 0xa9, 0x1e, 0x53, 0xbb, 0xb2,
            0x71, 0x15, 0xf4, 0x23, 0x5d, 0x82, 0x33, 0x44, 0xd1, 0x16,
            0x82, 0x04, 0x13, 0xb6, 0x30, 0xe7, 0x96, 0x4f, 0x22, 0xe0,
            0xec, 0xe0,
        ]);

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
                privateKey: privateKey),
            _fx.MakeTransaction(
                new[]
                {
                    DumbAction.Create((addresses[2], "baz"), (null, addresses[2], 100)),
                    DumbAction.Create((addresses[3], "qux"), (null, addresses[3], 100)),
                },
                timestamp: epoch.AddSeconds(5),
                nonce: 1,
                privateKey: privateKey),
        ];

        return (addresses, txs);
    }

    [Fact]
    public void TipChanged()
    {
        var genesisBlock = _blockChain.Genesis;
        TipChangedInfo? tipChangedInfo = null;
        var block = _blockChain.ProposeBlock(new PrivateKey());
        var blockCommit = CreateBlockCommit(block);
        using var subscription = _blockChain.TipChanged.Subscribe(i => tipChangedInfo = i);
        _blockChain.Append(block, CreateBlockCommit(block));
        Assert.NotNull(tipChangedInfo);
        Assert.Equal(block, tipChangedInfo.Tip);
        Assert.Equal(1, tipChangedInfo.Tip.Height);
        Assert.Throws<InvalidOperationException>(() => _blockChain.Append(block, blockCommit));
    }

    [Fact]
    public void CreateWithGenesisBlock()
    {
        using var fx = new MemoryRepositoryFixture(new());
        var addresses = ImmutableArray.Create(
            fx.Address1,
            fx.Address2,
            fx.Address3);

        var validatorKey = new PrivateKey();
        var proposerKey = new PrivateKey();
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
                Signer = proposerKey.Address,
                GenesisHash = default,
                Actions = new[] { systemAction }.ToBytecodes(),
            }.Sign(proposerKey))
            .ToArray();
        var customTxs = new[]
        {
            new TransactionMetadata
            {
                Nonce = systemTxs.Length,
                Signer = proposerKey.Address,
                Timestamp = DateTimeOffset.UtcNow,
                Actions = customActions.ToBytecodes(),
                MaxGasPrice = default,
            }.Sign(proposerKey),
        };
        var genesisBlock = new BlockBuilder
        {
            Transactions = [.. systemTxs.Concat(customTxs)],
        }.Create(proposerKey);
        var blockChain = new Libplanet.Blockchain(genesisBlock, fx.Repository, fx.Options);

        var validator = blockChain
            .GetWorld()
            .GetValidators()[0];
        Assert.Equal(validatorKey.Address, validator.Address);
        Assert.Equal(BigInteger.One, validator.Power);

        var states = addresses
            .Select(address => blockChain
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
        var privateKey = new PrivateKey();
        var txsA = Enumerable.Range(0, 3)
            .Select(nonce => _fx.MakeTransaction(
                nonce: nonce, privateKey: privateKey, timestamp: DateTimeOffset.Now))
            .ToArray();
        _blockChain.StagedTransactions.AddRange(txsA);
        Block b1 = _blockChain.ProposeBlock(privateKey);
        _blockChain.Append(b1, CreateBlockCommit(b1));
        Assert.Equal(txsA, b1.Transactions);

        var txsB = Enumerable.Range(0, 4)
            .Select(nonce => _fx.MakeTransaction(
                nonce: nonce, privateKey: privateKey, timestamp: DateTimeOffset.Now))
            .ToArray();
        _blockChain.StagedTransactions.AddRange(txsB);

        // Stage only txs having higher or equal with nonce than expected nonce.
        Assert.Single(_blockChain.StagedTransactions.Keys);
        // Assert.Single(_blockChain.StagedTransactions.Iterate(filtered: true));
        Assert.Equal(4, _blockChain.StagedTransactions.Count);
    }

    [Fact]
    private void CheckIfTxPolicyExceptionHasInnerException()
    {
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
        var genesisTxKey = new PrivateKey();
        var genesisTx = new TransactionMetadata
        {
            Signer = genesisTxKey.Address,
            Actions = [],
        }.Sign(genesisTxKey);
        var genesisWithTx = ProposeGenesis(GenesisProposer, transactions: [genesisTx]).Sign(GenesisProposer);

        var chain = new Libplanet.Blockchain(genesisWithTx, repository, options);

        var bockTxKey = new PrivateKey();
        var blockTx = new TransactionMetadata
        {
            Signer = bockTxKey.Address,
            Actions = Array.Empty<DumbAction>().ToBytecodes(),
        }.Sign(bockTxKey);
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
        var storeFixture = new MemoryRepositoryFixture();
        var options = storeFixture.Options;
        var repository = storeFixture.Repository;
        var addresses = ImmutableList<Address>.Empty
            .Add(storeFixture.Address1)
            .Add(storeFixture.Address2)
            .Add(storeFixture.Address3);

        var newValidatorPrivateKey = new PrivateKey();
        var newValidators = ValidatorPrivateKeys.Append(newValidatorPrivateKey).ToArray();
        var newValidatorPowers = TestUtils.Validators.Select(v => v.Power)
            .Append(BigInteger.One).ToArray();
        var initialValidatorSet =
            ValidatorPrivateKeys.Select(
                pk => new Validator { Address = pk.Address })
            .ToImmutableSortedSet();
        var systemActions = new[]
        {
            new Initialize
            {
                Validators = initialValidatorSet,
            },
        };
        var privateKey = new PrivateKey();
        var txs = systemActions
            .Select((systemAction, i) => new TransactionMetadata
            {
                Nonce = i,
                Signer = privateKey.Address,
                GenesisHash = default,
                Actions = new IAction[] { systemAction }.ToBytecodes(),
            }.Sign(privateKey))
            .ToImmutableList();
        var genesisBlock = new BlockBuilder
        {
            Transactions = [.. txs],
        }.Create(privateKey);

        var blockChain = new Libplanet.Blockchain(genesisBlock, repository, options);

        blockChain.StagedTransactions.Add(new PrivateKey(), submission: new()
        {
            Actions =
            [
                new SetValidator
                {
                    Validator = new Validator { Address = newValidatorPrivateKey.Address },
                },
            ],
        });
        var newBlock = blockChain.ProposeBlock(new PrivateKey());
        var newBlockCommit = new BlockCommit
        {
            Height = newBlock.Height,
            Round = 0,
            BlockHash = newBlock.BlockHash,
            Votes = [.. ValidatorPrivateKeys.Select(
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
        blockChain.Append(newBlock, newBlockCommit);

        blockChain.StagedTransactions.Add(new PrivateKey(), submission: new()
        {
            Actions =
            [
                new SetValidator
                {
                    Validator = new Validator { Address = new PrivateKey().Address },
                },
            ],
        });
        var nextBlock = blockChain.ProposeBlock(new PrivateKey());
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
        blockChain.Append(nextBlock, nextBlockCommit);

        blockChain.StagedTransactions.Add(new PrivateKey(), submission: new()
        {
            Actions = new[]
            {
                new SetValidator
                {
                    Validator = new Validator { Address = new PrivateKey().Address },
                },
            },
        });
        var invalidCommitBlock = blockChain.ProposeBlock(new PrivateKey());

        Assert.Throws<InvalidOperationException>(
            () => blockChain.Append(
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
            blockChain
                .GetWorld(0)
                .GetValidators(),
            [.. ValidatorPrivateKeys.Select(pk => new Validator { Address = pk.Address })]);

        Assert.Equal(
            blockChain
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
    //         BlockChain blockChain,
    //         Transaction transaction)
    //     {
    //         _hook(blockChain);
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
    //         BlockChain blockChain,
    //         Transaction transaction)
    //     {
    //         _hook(blockChain);
    //         return base.ValidateNextBlockTx(blockChain, transaction);
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
