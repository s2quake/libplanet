using System.Security.Cryptography;
using System.Threading.Tasks;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Action.Sys;
using Libplanet.Action.Tests.Common;
using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;
using Serilog;
using Xunit.Abstractions;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Blockchain;

public partial class BlockChainTest : IDisposable
{
    private readonly ILogger _logger;
    private readonly StoreFixture _fx;
    private readonly BlockChainOptions _policy;
    private BlockChain _blockChain;
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

        _policy = new BlockChainOptions
        {
            PolicyActions = new PolicyActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
            BlockOptions = new BlockOptions
            {
                MaxTransactionsBytes = 50 * 1024,
            },
        };

        _fx = GetStoreFixture(_policy);
        _blockChain = BlockChain.Create(_fx.GenesisBlock, _policy);
        _stagePolicy = _blockChain.StagedTransactions;

        _validNext = _blockChain.EvaluateAndSign(
            new RawBlock
            {
                Header = new BlockHeader
                {
                    Version = BlockHeader.CurrentProtocolVersion,
                    Height = 1,
                    Timestamp = _fx.GenesisBlock.Timestamp.AddSeconds(1),
                    Proposer = _fx.Proposer.Address,
                    PreviousHash = _fx.GenesisBlock.BlockHash,
                },
            },
            _fx.Proposer);
    }

    public void Dispose()
    {
        _fx.Dispose();
    }

    [Fact]
    public void CanonicalId()
    {
        var chain1 = _blockChain;
        var key = new PrivateKey();
        Block block1 = chain1.ProposeBlock(key);
        chain1.Append(block1, CreateBlockCommit(block1));
        Block block2 = chain1.ProposeBlock(key);
        chain1.Append(block2, CreateBlockCommit(block2));
        Assert.Equal(chain1.Id, _fx.Store.ChainId);
        Assert.Equal(chain1.Id, _fx.Store.ChainId);

        var beginActions = ImmutableArray.Create<IAction>();
        var endActions = ImmutableArray.Create<IAction>(
            new MinerReward(1));

        var policy = new BlockChainOptions
        {
            PolicyActions = new PolicyActions
            {
                BeginBlockActions = beginActions,
                EndBlockActions = endActions,
            },
        };
        var z = new BlockChain(_fx.GenesisBlock, policy);

        Assert.Equal(chain1.Id, z.Id);
    }

    [Fact]
    public void Validators()
    {
        var validatorSet = _blockChain
            .GetNextWorld()
            .GetValidatorSet();
        _logger.Debug(
            "GenesisBlock is {Hash}, Transactions: {Txs}",
            _blockChain.Genesis,
            _blockChain.Genesis.Transactions);
        Assert.Equal(TestUtils.Validators.Count, validatorSet.Count);
    }

    [Fact]
    public void CanFindBlockByIndex()
    {
        var genesis = _blockChain.Genesis;
        Assert.Equal(genesis, _blockChain.Blocks[0]);

        Block block = _blockChain.ProposeBlock(new PrivateKey());
        _blockChain.Append(block, TestUtils.CreateBlockCommit(block));
        Assert.Equal(block, _blockChain.Blocks[1]);
    }

    [Fact]
    public void BlockHashes()
    {
        var key = new PrivateKey();
        var genesis = _blockChain.Genesis;

        Assert.Single(_blockChain.Blocks.Keys);

        Block b1 = _blockChain.ProposeBlock(key);
        _blockChain.Append(b1, CreateBlockCommit(b1));
        Assert.Equal(new[] { genesis.BlockHash, b1.BlockHash }, _blockChain.Blocks.Keys);

        Block b2 = _blockChain.ProposeBlock(key);
        _blockChain.Append(b2, CreateBlockCommit(b2));
        Assert.Equal(
            new[] { genesis.BlockHash, b1.BlockHash, b2.BlockHash },
            _blockChain.Blocks.Keys);

        Block b3 = _blockChain.ProposeBlock(key);
        _blockChain.Append(b3, CreateBlockCommit(b3));
        Assert.Equal(
            new[] { genesis.BlockHash, b1.BlockHash, b2.BlockHash, b3.BlockHash },
            _blockChain.Blocks.Keys);
    }

    [Fact]
    public void ProcessActions()
    {
        var repository = new Repository();
        var blockChainStates = new BlockChainStates(repository);
        var policy = new BlockChainOptions();
        var actionEvaluator = new ActionEvaluator(
            repository.StateStore,
            policy.PolicyActions);
        var nonce = 0;
        var txs = TestUtils.Validators
            .Select(validator => new TransactionMetadata
            {
                Nonce = nonce++,
                Signer = GenesisProposer.Address,
                Actions = new IAction[]
                    {
                        new Initialize
                        {
                            Validators = TestUtils.Validators,
                            States = ImmutableDictionary.Create<Address, object>(),
                        },
                    }.ToBytecodes(),
            }.Sign(GenesisProposer))
            .OrderBy(tx => tx.Id)
            .ToImmutableList();
        var genesis = BlockChain.ProposeGenesisBlock(GenesisProposer, transactions: [.. txs]);
        var chain = BlockChain.Create(genesis, policy);
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
        Block block1 = chain.ProposeBlock(new PrivateKey());
        chain.Append(block1, CreateBlockCommit(block1));
        var result = (BattleResult)chain
            .GetNextWorld()
            .GetAccount(ReservedAddresses.LegacyAccount)
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
            .GetNextWorld()
            .GetAccount(ReservedAddresses.LegacyAccount)
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
            .GetNextWorld()
            .GetAccount(ReservedAddresses.LegacyAccount)
            .GetValue(_fx.Address1);
    }

    [Fact]
    public void ActionRenderersHaveDistinctContexts()
    {
        var options = new BlockChainOptions();
        var generatedRandomValueLogs = new List<int>();
        BlockChain blockChain = MakeBlockChain(options);
        var privateKey = new PrivateKey();
        var action = DumbAction.Create((default, string.Empty));
        var actions = new[] { action };
        blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKey,
            Actions = actions,
        });
        Block block = blockChain.ProposeBlock(new PrivateKey());

        generatedRandomValueLogs.Clear();
        Assert.Empty(generatedRandomValueLogs);
        blockChain.Append(block, CreateBlockCommit(block));
        Assert.Equal(2, generatedRandomValueLogs.Count);
        Assert.Equal(generatedRandomValueLogs[0], generatedRandomValueLogs[1]);
    }

    [Fact]
    public void RenderActionsAfterBlockIsRendered()
    {
        var options = new BlockChainOptions();
        var blockChain = MakeBlockChain(options);
        var privateKey = new PrivateKey();

        var action = DumbAction.Create((default, string.Empty));
        var actions = new[] { action };
        blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKey,
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
        var policy = new BlockChainOptions();
        var store = new Libplanet.Store.Repository(new MemoryDatabase());
        var stateStore = new TrieStateStore();

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
        BlockChain blockChain = MakeBlockChain(policy);
        var privateKey = new PrivateKey();

        var action = DumbAction.Create((default, string.Empty));
        var actions = new[] { action };
        blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKey,
            Actions = actions,
        });
        Block block = blockChain.ProposeBlock(new PrivateKey());

        ThrowException.SomeException e = Assert.Throws<ThrowException.SomeException>(
            () => blockChain.Append(block, CreateBlockCommit(block)));
        Assert.Equal("thrown by renderer", e.Message);
        Assert.Equal(2, blockChain.Blocks.Count);
    }

    [Fact]
    public void FindNextHashes()
    {
        var key = new PrivateKey();
        IReadOnlyList<BlockHash> hashes;

        hashes = _blockChain.FindNextHashes(new BlockLocator(_blockChain.Genesis.BlockHash));
        Assert.Single(hashes);
        Assert.Equal(_blockChain.Genesis.BlockHash, hashes.First());
        var block0 = _blockChain.Genesis;
        var block1 = _blockChain.ProposeBlock(key);
        _blockChain.Append(block1, CreateBlockCommit(block1));
        var block2 = _blockChain.ProposeBlock(key);
        _blockChain.Append(block2, CreateBlockCommit(block2));
        var block3 = _blockChain.ProposeBlock(key);
        _blockChain.Append(block3, CreateBlockCommit(block3));

        hashes = _blockChain.FindNextHashes(new BlockLocator(block0.BlockHash));
        Assert.Equal(new[] { block0.BlockHash, block1.BlockHash, block2.BlockHash, block3.BlockHash }, hashes);

        hashes = _blockChain.FindNextHashes(new BlockLocator(block1.BlockHash));
        Assert.Equal(new[] { block1.BlockHash, block2.BlockHash, block3.BlockHash }, hashes);

        hashes = _blockChain.FindNextHashes(new BlockLocator(block0.BlockHash), count: 2);
        Assert.Equal(new[] { block0.BlockHash, block1.BlockHash }, hashes);
    }

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

        BlockLocator actual = _blockChain.GetBlockLocator();
        BlockLocator expected = new BlockLocator(blocks[9].BlockHash);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetBlockCommit()
    {
        // Note: Getting BlockCommit from PoW block test is not present.
        // Requesting blockCommit of genesis block returns null.
        Assert.Null(_blockChain.BlockCommits[0]);
        Assert.Null(_blockChain.BlockCommits[_blockChain.Genesis.BlockHash]);

        // BlockCommit is put to store when block is appended.
        Block block1 = _blockChain.ProposeBlock(new PrivateKey());
        BlockCommit blockCommit1 = CreateBlockCommit(block1);
        _blockChain.Append(block1, blockCommit1);
        Assert.Equal(blockCommit1, _blockChain.BlockCommits[block1.Height]);
        Assert.Equal(blockCommit1, _blockChain.BlockCommits[block1.BlockHash]);

        // BlockCommit is retrieved from lastCommit.
        Block block2 = _blockChain.ProposeBlock(new PrivateKey());
        BlockCommit blockCommit2 = CreateBlockCommit(block2);
        _blockChain.Append(block2, blockCommit2);

        // These are different due to timestamps on votes.
        Assert.NotEqual(blockCommit1, _blockChain.BlockCommits[block1.Height]);
        Assert.Equal(block2.LastCommit, _blockChain.BlockCommits[block1.Height]);
        Assert.Equal(block2.LastCommit, _blockChain.BlockCommits[block1.BlockHash]);
    }

    [Fact]
    public void CleanupBlockCommitStore()
    {
        BlockCommit blockCommit1 = CreateBlockCommit(
            new BlockHash(GetRandomBytes(BlockHash.Size)), 1, 0);
        BlockCommit blockCommit2 = CreateBlockCommit(
            new BlockHash(GetRandomBytes(BlockHash.Size)), 2, 0);
        BlockCommit blockCommit3 = CreateBlockCommit(
            new BlockHash(GetRandomBytes(BlockHash.Size)), 3, 0);

        _blockChain.Store.BlockCommits.Add(blockCommit1);
        _blockChain.Store.BlockCommits.Add(blockCommit2);
        _blockChain.Store.BlockCommits.Add(blockCommit3);
        _blockChain.CleanupBlockCommitStore(blockCommit3.Height);

        Assert.Null(_blockChain.Store.BlockCommits[blockCommit1.BlockHash]);
        Assert.Null(_blockChain.Store.BlockCommits[blockCommit2.BlockHash]);
        Assert.Equal(blockCommit3, _blockChain.Store.BlockCommits[blockCommit3.BlockHash]);
    }

    [Fact]
    public void GetStatesOnCreatingBlockChain()
    {
        bool invoked = false;
        var policy = new BlockChainOptions
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
        Libplanet.Store.Repository store = new Libplanet.Store.Repository(new MemoryDatabase());
        var stateStore = new TrieStateStore();
        var actionEvaluator = new ActionEvaluator(
            stateStore,
            policy.PolicyActions);
        var txKey = new PrivateKey();
        Block genesisWithTx = ProposeGenesisBlock(
            ProposeGenesis(
                GenesisProposer.PublicKey,
                [
                    new TransactionMetadata
                    {
                        Signer = txKey.Address,
                        Actions = [],
                    }.Sign(txKey),
                ]),
            GenesisProposer);
        var chain = BlockChain.Create(genesisWithTx, policy);
        Assert.False(invoked);
    }

    // This is a regression test for:
    // https://github.com/planetarium/libplanet/issues/189#issuecomment-482443607.
    [Fact]
    public void GetStateOnlyDrillsDownUntilRequestedAddressesAreFound()
    {
        var policy = new BlockChainOptions();
        // var tracker = new StoreTracker(_fx.Store);
        var chain = new BlockChain(_fx.GenesisBlock, policy);

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
                    .GetNextWorld()
                    .GetAccount(ReservedAddresses.LegacyAccount)
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
        var policy = new BlockChainOptions();
        // var tracker = new StoreTracker(_fx.Store);
        var chain = new BlockChain(_fx.GenesisBlock, policy);
        Block b = chain.Genesis;
        for (int i = 0; i < 20; ++i)
        {
            b = chain.ProposeBlock(_fx.Proposer);
            chain.Append(b, CreateBlockCommit(b));
        }

        // tracker.ClearLogs();
        Address nonexistent = new PrivateKey().Address;
        var result = chain
            .GetNextWorld()
            .GetAccount(ReservedAddresses.LegacyAccount)
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
        var policy = new BlockChainOptions();
        var chain = new BlockChain(_fx.GenesisBlock, policy);

        Assert.All(
            addresses.Select(
                address => chain
                    .GetNextWorld()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(address)),
            Assert.Null);
        foreach (var address in addresses)
        {
            Assert.Null(chain
                .GetNextWorld()
                .GetAccount(ReservedAddresses.LegacyAccount)
                .GetValue(address));
        }

        var privateKeysAndAddresses10 = privateKeys.Zip(addresses, (k, a) => (k, a));
        foreach (var (key, address) in privateKeysAndAddresses10)
        {
            chain.StagedTransactions.Add(submission: new()
            {
                Signer = key,
                Actions = [DumbAction.Create((address, "1"))],
            });
        }

        Block block1 = chain.ProposeBlock(privateKeys[0]);

        chain.Append(block1, CreateBlockCommit(block1));

        Assert.All(
            addresses.Select(
                address => chain
                    .GetNextWorld()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(address)),
            v => Assert.Equal("1", v));
        foreach (var address in addresses)
        {
            Assert.Equal(
                "1",
                chain
                    .GetNextWorld()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(address));
        }

        chain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKeys[0],
            Actions = new[] { DumbAction.Create((addresses[0], "2")) },
        });
        Block block2 = chain.ProposeBlock(privateKeys[0]);
        chain.Append(block2, CreateBlockCommit(block2));
        Assert.Equal(
            "1,2",
            chain
                .GetNextWorld()
                .GetAccount(ReservedAddresses.LegacyAccount)
                .GetValue(addresses[0]));
        Assert.All(
            addresses.Skip(1).Select(
                address => chain
                    .GetNextWorld()
                    .GetAccount(ReservedAddresses.LegacyAccount)
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

        var emptyLocator = new BlockLocator(_blockChain.Genesis.BlockHash);
        var invalidLocator = new BlockLocator(
            new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)));
        var locator = new BlockLocator(b4.BlockHash);

        using (var emptyFx = new MemoryStoreFixture(_policy))
        using (var forkFx = new MemoryStoreFixture(_policy))
        {
            var emptyChain = BlockChain.Create(emptyFx.GenesisBlock, _blockChain.Options);
            var fork = BlockChain.Create(forkFx.GenesisBlock, _blockChain.Options);
            fork.Append(b1, CreateBlockCommit(b1));
            fork.Append(b2, CreateBlockCommit(b2));
            Block b5 = fork.ProposeBlock(key);
            fork.Append(b5, CreateBlockCommit(b5));

            // Testing emptyChain
            Assert.Equal(_blockChain.Genesis.BlockHash, emptyChain.FindBranchpoint(emptyLocator));
            Assert.Null(emptyChain.FindBranchpoint(invalidLocator));
            Assert.Null(emptyChain.FindBranchpoint(locator));

            // Testing _blockChain
            Assert.Equal(_blockChain.Genesis.BlockHash, _blockChain.FindBranchpoint(emptyLocator));
            Assert.Null(_blockChain.FindBranchpoint(invalidLocator));
            Assert.Equal(b4.BlockHash, _blockChain.FindBranchpoint(locator));

            // Testing fork
            Assert.Equal(_blockChain.Genesis.BlockHash, fork.FindBranchpoint(emptyLocator));
            Assert.Null(fork.FindBranchpoint(invalidLocator));
            Assert.Null(fork.FindBranchpoint(locator));
        }
    }

    [Fact]
    public void GetNextTxNonce()
    {
        var privateKey = new PrivateKey();
        Address address = privateKey.Address;
        var actions = new[] { DumbAction.Create((_fx.Address1, "foo")) };
        var genesis = _blockChain.Genesis;

        Assert.Equal(0, _blockChain.GetNextTxNonce(address));

        Transaction[] txsA =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 0),
        ];

        Block b1 = _blockChain.ProposeBlock(_fx.Proposer);
        _blockChain.Append(b1, CreateBlockCommit(b1));

        Assert.Equal(1, _blockChain.GetNextTxNonce(address));

        Transaction[] txsB =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 1),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 2),
        ];

        StageTransactions(txsB);

        Assert.Equal(3, _blockChain.GetNextTxNonce(address));

        Transaction[] txsC =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 3),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 3),
        ];
        StageTransactions(txsC);

        Assert.Equal(4, _blockChain.GetNextTxNonce(address));

        Transaction[] txsD =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 5),
        ];
        StageTransactions(txsD);

        Assert.Equal(4, _blockChain.GetNextTxNonce(address));

        Transaction[] txsE =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 4),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 5),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 7),
        ];
        StageTransactions(txsE);

        foreach (var tx in _blockChain.StagedTransactions.Iterate())
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

        StageTransactions(txs);
        Block block = _blockChain.ProposeBlock(privateKey);
        _blockChain.Append(block, CreateBlockCommit(block));

        Transaction[] staleTxs =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 0),
            _fx.MakeTransaction(actions, privateKey: privateKey, nonce: 1),
        ];
        StageTransactions(staleTxs);

        Assert.Equal(2, _blockChain.GetNextTxNonce(address));

        _blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKey,
            Actions = actions,
        });
        Assert.Equal(3, _blockChain.GetNextTxNonce(address));

        _blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKey,
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

        Block ProposeNext(
            Block block,
            ImmutableSortedSet<Transaction> txs) =>
            _blockChain.EvaluateAndSign(
                TestUtils.ProposeNext(
                    block,
                    txs,
                    blockInterval: TimeSpan.FromSeconds(10),
                    proposer: _fx.Proposer.PublicKey,
                    lastCommit: CreateBlockCommit(block)),
                _fx.Proposer);

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
            Validators = [Validator.Create(new PrivateKey().Address, 1)],
            States = new Dictionary<Address, object>
            {
                [default] = "initial value",
            }.ToImmutableDictionary(),
        };

        _blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKey,
            Actions = [action],
        });
        _blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKey,
            Actions = [action],
        });

        List<Transaction> txs = _stagePolicy
            .Iterate()
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

        _blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKey,
            Actions = actions,
        });
        _blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKey,
            Actions = actions,
        });

        List<Transaction> txs = _stagePolicy
            .Iterate()
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
        Address address = privateKey.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => _blockChain.StagedTransactions.Add(submission: new()
            {
                Signer = privateKey,
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
            .GetNextWorld()
            .GetAccount(ReservedAddresses.LegacyAccount)
            .GetValue(miner1.Address);
        var miner2state = (int)_blockChain
            .GetNextWorld()
            .GetAccount(ReservedAddresses.LegacyAccount)
            .GetValue(miner2.Address);
        var rewardState = (string)_blockChain
            .GetNextWorld()
            .GetAccount(ReservedAddresses.LegacyAccount)
            .GetValue(rewardRecordAddress);

        Assert.Equal(2, miner1state);
        Assert.Equal(1, miner2state);

        Assert.Equal(
            $"{miner0},{miner1.Address},{miner1.Address},{miner2.Address}",
            rewardState);
    }

    /// Builds a fixture that has incomplete states for blocks other
    /// than the tip, to test <c>GetState()</c> method's
    /// <c>completeStates: true</c> option.
    ///
    /// <para>The fixture this makes has total 5 addresses (i.e., accounts;
    /// these go to the second item of the returned triple) and 11 blocks
    /// (these go to the third item of the returned triple). Every block
    /// contains a transaction with an action that mutates one account
    /// state except for the genesis block.  All transactions in the fixture
    /// are signed by one private key (its address goes to the first item
    /// of the returned triple).  The most important thing is that
    /// overall blocks in the fixture look like:</para>
    ///
    /// <code>
    ///  Index   UpdatedAddresses   States in Store
    /// ------- ------------------ -----------------
    ///      0                      Absent
    ///      1   addresses[0]       Absent
    ///      2   addresses[1]       Absent
    ///      3   addresses[2]       Absent
    ///      4   addresses[3]       Present
    ///      5   addresses[4]       Absent
    ///      6   addresses[0]       Absent
    ///      7   addresses[1]       Present
    ///      8   addresses[2]       Absent
    ///      9   addresses[3]       Absent
    ///     10   addresses[4]       Absent
    /// </code>
    /// </summary>
    /// <param name="store">store.</param>
    /// <param name="stateStore">State Store.</param>
    /// <returns>Tuple of addresses and chain.</returns>
    internal static (Address, Address[] Addresses, BlockChain Chain)
        MakeIncompleteBlockStates(Libplanet.Store.Repository store, TrieStateStore stateStore)
    {
        List<int> presentIndices = new List<int>() { 4, 7 };
        List<Block> presentBlocks = new List<Block>();

        var blockPolicy = new BlockChainOptions();
        // store = new StoreTracker(store);
        Guid chainId = Guid.NewGuid();
        var actionEvaluator = new ActionEvaluator(
            stateStore: stateStore,
            blockPolicy.PolicyActions);
        Block genesisBlock = ProposeGenesisBlock(
            ProposeGenesis(GenesisProposer.PublicKey),
            GenesisProposer);
        var chain = BlockChain.Create(genesisBlock, blockPolicy);
        var privateKey = new PrivateKey();
        Address signer = privateKey.Address;

        void BuildIndex(Guid id, Block block)
        {
            var chain = store.Chains[id];
            foreach (Transaction tx in block.Transactions)
            {
                chain.Nonces.Increase(tx.Signer);
            }

            // store.AppendIndex(id, block.BlockHash);
        }

        // Build a store with incomplete states
        Block b = chain.Genesis;
        World previousState = stateStore.GetWorld(default);
        const int accountsCount = 5;
        Address[] addresses = Enumerable.Repeat<object?>(null, accountsCount)
            .Select(_ => new PrivateKey().Address)
            .ToArray();
        for (int i = 0; i < 2; ++i)
        {
            for (int j = 0; j < accountsCount; ++j)
            {
                int index = (i * accountsCount) + j;
                Transaction tx = new TransactionMetadata
                {
                    Nonce = store.GetNonce(chain.Id, signer),
                    Signer = privateKey.Address,
                    GenesisHash = chain.Genesis.BlockHash,
                    Actions = new[] { DumbAction.Create((addresses[j], index.ToString())) }.ToBytecodes(),
                }.Sign(privateKey);
                b = chain.EvaluateAndSign(
                    ProposeNext(
                        b,
                        [tx],
                        blockInterval: TimeSpan.FromSeconds(10),
                        proposer: GenesisProposer.PublicKey,
                        lastCommit: CreateBlockCommit(b)),
                    GenesisProposer);

                var evals = actionEvaluator.EvaluateBlock((RawBlock)b, previousState);
                var dirty = evals[^1].OutputWorld.Trie
                    .Diff(evals.First().InputWorld.Trie)
                    .ToList();
                Assert.NotEmpty(dirty);
                chain.Blocks.Add(b);
                BuildIndex(chain.Id, b);
                Assert.Equal(b, chain.Blocks[b.BlockHash]);
                if (presentIndices.Contains((int)b.Height))
                {
                    presentBlocks.Add(b);
                }
            }
        }

        TrieStateStore incompleteStateStore = new TrieStateStore();
        ((TrieStateStore)stateStore).CopyStates(
            ImmutableHashSet<HashDigest<SHA256>>.Empty
                .Add(presentBlocks[0].StateRootHash)
                .Add(presentBlocks[1].StateRootHash),
            (TrieStateStore)incompleteStateStore);

        chain = new BlockChain(genesisBlock, blockPolicy);

        return (signer, addresses, chain);
    }

    /// Configures the store fixture that every test in this class depends on.
    /// Subclasses should override this.
    /// </summary>
    /// <param name="policyActions">The policy block actions to use.</param>
    /// <returns>The store fixture that every test in this class depends on.</returns>
    protected virtual StoreFixture GetStoreFixture(
        BlockChainOptions? options = null)
        => new MemoryStoreFixture(options ?? new());

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
    private void TipChanged()
    {
        var genesis = _blockChain.Genesis;

        // _renderer.ResetRecords();

        // Assert.Empty(_renderer.BlockRecords);
        Block block = _blockChain.ProposeBlock(new PrivateKey());
        _blockChain.Append(block, CreateBlockCommit(block));
        // IReadOnlyList<RenderRecord.BlockEvent> records = _renderer.BlockRecords;
        // Assert.Equal(2, records.Count);
        // foreach (RenderRecord.BlockEvent record in records)
        // {
        //     Assert.Equal(genesis, record.OldTip);
        //     Assert.Equal(block, record.NewTip);
        //     Assert.Equal(1, record.NewTip.Height);
        // }

        // _renderer.ResetRecords();
        Assert.Throws<InvalidOperationException>(
            () => _blockChain.Append(block, CreateBlockCommit(block)));
        // Assert.Empty(_renderer.BlockRecords);
    }

    [Fact]
    private void CreateWithGenesisBlock()
    {
        var storeFixture = new MemoryStoreFixture(new());
        var addresses = ImmutableList<Address>.Empty
            .Add(storeFixture.Address1)
            .Add(storeFixture.Address2)
            .Add(storeFixture.Address3);

        var validatorPrivKey = new PrivateKey();

        var privateKey = new PrivateKey();
        var systemActions = new IAction[]
        {
            new Initialize
            {
                States = ImmutableDictionary.Create<Address, object>(),
                Validators = ImmutableSortedSet.Create(
                [
                    Validator.Create(validatorPrivKey.Address, BigInteger.One),
                ]),
            },
        };

        var customActions =
            addresses
                .Select((address, index) => DumbAction.Create((address, index.ToString())))
                .ToArray();

        var systemTxs = systemActions
            .Select((systemAction, i) => new TransactionMetadata
            {
                Nonce = i,
                Signer = privateKey.Address,
                GenesisHash = default,
                Actions = new[] { systemAction }.ToBytecodes(),
            }.Sign(privateKey))
            .ToArray();
        var customTxs = new[]
        {
            new TransactionMetadata
            {
                        Nonce = systemTxs.Length,
                        Signer = privateKey.Address,
                        Timestamp = DateTimeOffset.UtcNow,
                        Actions = customActions.ToBytecodes(),
                        MaxGasPrice = default,
            }.Sign(privateKey),
        };
        var txs = systemTxs.Concat(customTxs).ToImmutableList();
        var genesisBlock = BlockChain.ProposeGenesisBlock(
            proposer: privateKey,
            transactions: [.. txs]);
        var blockChain = BlockChain.Create(genesisBlock, storeFixture.Options);

        var validator = blockChain
            .GetNextWorld()
            .GetValidatorSet()[0];
        Assert.Equal(validatorPrivKey.Address, validator.Address);
        Assert.Equal(BigInteger.One, validator.Power);

        var states = addresses
            .Select(address => blockChain
                .GetNextWorld()
                .GetAccount(ReservedAddresses.LegacyAccount)
                .GetValue(address))
            .ToArray();
        for (var i = 0; i < states.Length; ++i)
        {
            Assert.Equal(states[i], i.ToString());
        }
    }

    [Fact]
    private void ConstructWithUnexpectedGenesisBlock()
    {
        var policy = new BlockChainOptions();
        Libplanet.Store.Repository store = new Libplanet.Store.Repository(new MemoryDatabase());
        var stateStore = new TrieStateStore();

        var genesisBlockA = BlockChain.ProposeGenesisBlock(new PrivateKey(), []);
        var genesisBlockB = BlockChain.ProposeGenesisBlock(new PrivateKey(), []);

        var blockChain = BlockChain.Create(genesisBlockA, policy);

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = new BlockChain(genesisBlockB, policy);
        });
    }

    [Fact]
    private void FilterLowerNonceTxAfterStaging()
    {
        var privateKey = new PrivateKey();
        var txsA = Enumerable.Range(0, 3)
            .Select(nonce => _fx.MakeTransaction(
                nonce: nonce, privateKey: privateKey, timestamp: DateTimeOffset.Now))
            .ToArray();
        StageTransactions(txsA);
        Block b1 = _blockChain.ProposeBlock(privateKey);
        _blockChain.Append(b1, CreateBlockCommit(b1));
        Assert.Equal(txsA, b1.Transactions);

        var txsB = Enumerable.Range(0, 4)
            .Select(nonce => _fx.MakeTransaction(
                nonce: nonce, privateKey: privateKey, timestamp: DateTimeOffset.Now))
            .ToArray();
        StageTransactions(txsB);

        // Stage only txs having higher or equal with nonce than expected nonce.
        Assert.Single(_blockChain.StagedTransactions.Keys);
        Assert.Single(_blockChain.StagedTransactions.Iterate(filtered: true));
        Assert.Equal(4, _blockChain.StagedTransactions.Iterate(filtered: false).Count());
    }

    [Fact]
    private void CheckIfTxPolicyExceptionHasInnerException()
    {
        // var policy = new NullPolicyButTxPolicyAlwaysThrows(
        //     x =>
        //     {
        //         // ReSharper disable AccessToModifiedClosure
        //         // The following method calls should not throw any exceptions:
        //         x?.GetNextWorld()
        //             .GetAccount(ReservedAddresses.LegacyAccount)
        //             .GetValue(default);
        //         x?.GetNextWorld()
        //             .GetAccount(ReservedAddresses.LegacyAccount)
        //             .GetValue(default);
        //         // ReSharper restore AccessToModifiedClosure
        //     });
        var policy = new BlockChainOptions();
        Libplanet.Store.Repository store = new Libplanet.Store.Repository(new MemoryDatabase());
        var stateStore = new TrieStateStore();
        var genesisTxKey = new PrivateKey();
        var genesisTx = new TransactionMetadata
        {
            Signer = genesisTxKey.Address,
            Actions = [],
        }.Sign(genesisTxKey);
        var actionEvaluator = new ActionEvaluator(
            stateStore,
            policy.PolicyActions);
        var genesisWithTx = ProposeGenesisBlock(
            ProposeGenesis(GenesisProposer.PublicKey, [genesisTx]),
            privateKey: GenesisProposer);

        var chain = BlockChain.Create(genesisWithTx, policy);

        var bockTxKey = new PrivateKey();
        var blockTx = new TransactionMetadata
        {
            Signer = bockTxKey.Address,
            Actions = Array.Empty<DumbAction>().ToBytecodes(),
        }.Sign(bockTxKey);
        var nextStateRootHash = chain.GetNextStateRootHash(genesisWithTx.BlockHash);
        var block = ProposeNextBlock(
            previousBlock: chain.Genesis,
            proposer: GenesisProposer,
            txs: [blockTx],
            stateRootHash: (HashDigest<SHA256>)nextStateRootHash);

        var e = Assert.Throws<InvalidOperationException>(
            () => chain.Append(block, CreateBlockCommit(block)));
        Assert.NotNull(e.InnerException);
    }

    [Fact]
    private void ValidateNextBlockCommitOnValidatorSetChange()
    {
        var storeFixture = new MemoryStoreFixture();
        var options = new BlockChainOptions();

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
                pk => Validator.Create(pk.Address, BigInteger.One))
            .ToImmutableSortedSet();
        var systemActions = new[]
        {
            new Initialize
            {
                Validators = initialValidatorSet,
                States = ImmutableDictionary.Create<Address, object>(),
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

        var actionEvaluator = new ActionEvaluator(
            options.Repository.StateStore,
            options.PolicyActions);
        Block genesis = BlockChain.ProposeGenesisBlock(
            proposer: privateKey,
            transactions: [.. txs]);
        BlockChain blockChain = BlockChain.Create(genesis, options);

        blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = new PrivateKey(),
            Actions =
            [
                new SetValidator
                {
                    Validator = Validator.Create(newValidatorPrivateKey.Address),
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
                    Flag = VoteFlag.PreCommit,
                }.Sign(pk))
            .OrderBy(vote => vote.Validator)],
        };
        blockChain.Append(newBlock, newBlockCommit);

        blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = new PrivateKey(),
            Actions =
            [
                new SetValidator
                {
                    Validator = Validator.Create(new PrivateKey().Address),
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
                        Flag = VoteFlag.PreCommit,
                    }.Sign(newValidators[index]))
                .OrderBy(vote => vote.Validator)],
        };
        blockChain.Append(nextBlock, nextBlockCommit);

        blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = new PrivateKey(),
            Actions = new[]
            {
                new SetValidator
                {
                    Validator = Validator.Create(new PrivateKey().Address),
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
                                Flag = VoteFlag.PreCommit,
                            }.Sign(newValidators[index]))],
                }));

        Assert.Equal(
            blockChain
                .GetNextWorldState(0)
                .GetValidatorSet(),
            [.. ValidatorPrivateKeys.Select(pk => Validator.Create(pk.Address, BigInteger.One))]);

        Assert.Equal(
            blockChain
                .GetNextWorldState(1)
                .GetValidatorSet(),
            [.. newValidators.Select(pk => Validator.Create(pk.Address, BigInteger.One))]);
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
