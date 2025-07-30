using System.Security.Cryptography;
using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Action.Sys;
using Libplanet.Action.Tests.Common;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Blockchain.Renderers.Debug;
using Libplanet.Serialization;
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
    private StoreFixture _fx;
    private BlockPolicy _policy;
    private BlockChain _blockChain;
    private ValidatingActionRenderer _renderer;
    private Block _validNext;
    private IStagePolicy _stagePolicy;

    public BlockChainTest(ITestOutputHelper output)
    {
        Log.Logger = _logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithThreadId()
            .WriteTo.TestOutput(output)
            .CreateLogger()
            .ForContext<BlockChainTest>();

        _policy = new BlockPolicy(
            new PolicyActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
            getMaxTransactionsBytes: _ => 50 * 1024);
        _stagePolicy = new VolatileStagePolicy();
        _fx = GetStoreFixture(_policy.PolicyActions);
        _renderer = new ValidatingActionRenderer();
        _blockChain = BlockChain.Create(
            _policy,
            _stagePolicy,
            _fx.Store,
            _fx.StateStore,
            _fx.GenesisBlock,
            new ActionEvaluator(
                stateStore: _fx.StateStore,
                _policy.PolicyActions),
            renderers: new[] { new LoggedActionRenderer(_renderer, Log.Logger) }
        );
        _renderer.ResetRecords();

        _validNext = _blockChain.EvaluateAndSign(
            RawBlock.Propose(
                new BlockMetadata
                {
                    ProtocolVersion = BlockMetadata.CurrentProtocolVersion,
                    Height = 1,
                    Timestamp = _fx.GenesisBlock.Timestamp.AddSeconds(1),
                    Proposer = _fx.Proposer.Address,
                    PreviousHash = _fx.GenesisBlock.Hash,
                }),
            _fx.Proposer);
    }

    public void Dispose()
    {
        _fx.Dispose();
    }

    [SkippableFact]
    public void CanonicalId()
    {
        var chain1 = _blockChain;
        var key = new PrivateKey();
        Block block1 = chain1.ProposeBlock(key);
        chain1.Append(block1, CreateBlockCommit(block1));
        Block block2 = chain1.ProposeBlock(key, CreateBlockCommit(chain1.Tip));
        chain1.Append(block2, CreateBlockCommit(block2));
        Assert.Equal(chain1.Id, _fx.Store.GetCanonicalChainId());
        Assert.Equal(chain1.Id, _fx.Store.GetCanonicalChainId());

        var beginActions = ImmutableArray.Create<IAction>(
        );
        var endActions = ImmutableArray.Create<IAction>(
            new MinerReward(1)
        );

        var policy = new BlockPolicy(
            new PolicyActions
            {
                BeginBlockActions = beginActions,
                EndBlockActions = endActions,
            });
        var blockChainStates = new BlockChainStates(_fx.Store, _fx.StateStore);
        var z = new BlockChain(
            policy,
            new VolatileStagePolicy(),
            _fx.Store,
            _fx.StateStore,
            _fx.GenesisBlock,
            blockChainStates,
            new ActionEvaluator(
                _fx.StateStore,
                policy.PolicyActions));

        Assert.Equal(chain1.Id, z.Id);
    }

    [SkippableFact]
    public void Validators()
    {
        var validatorSet = _blockChain
            .GetNextWorldState()
            .GetValidatorSet();
        _logger.Debug(
            "GenesisBlock is {Hash}, Transactions: {Txs}",
            _blockChain.Genesis,
            _blockChain.Genesis.Transactions);
        Assert.Equal(TestUtils.Validators.Count, validatorSet.Count);
    }

    [SkippableFact]
    public void CanFindBlockByIndex()
    {
        var genesis = _blockChain.Genesis;
        Assert.Equal(genesis, _blockChain[0]);

        Block block = _blockChain.ProposeBlock(new PrivateKey());
        _blockChain.Append(block, TestUtils.CreateBlockCommit(block));
        Assert.Equal(block, _blockChain[1]);
    }

    [SkippableFact]
    public void BlockHashes()
    {
        var key = new PrivateKey();
        var genesis = _blockChain.Genesis;

        Assert.Single(_blockChain.BlockHashes);

        Block b1 = _blockChain.ProposeBlock(key);
        _blockChain.Append(b1, CreateBlockCommit(b1));
        Assert.Equal(new[] { genesis.Hash, b1.Hash }, _blockChain.BlockHashes);

        Block b2 = _blockChain.ProposeBlock(
            key, CreateBlockCommit(_blockChain.Tip));
        _blockChain.Append(b2, CreateBlockCommit(b2));
        Assert.Equal(
            new[] { genesis.Hash, b1.Hash, b2.Hash },
            _blockChain.BlockHashes
        );

        Block b3 = _blockChain.ProposeBlock(
            key, CreateBlockCommit(_blockChain.Tip));
        _blockChain.Append(b3, CreateBlockCommit(b3));
        Assert.Equal(
            new[] { genesis.Hash, b1.Hash, b2.Hash, b3.Hash },
            _blockChain.BlockHashes
        );
    }

    [SkippableFact]
    public void ProcessActions()
    {
        var store = new MemoryStore();
        var stateStore = new TrieStateStore();
        var blockChainStates = new BlockChainStates(store, stateStore);
        var policy = new BlockPolicy();
        var actionEvaluator = new ActionEvaluator(
            stateStore,
            policy.PolicyActions);
        var nonce = 0;
        var txs = TestUtils.Validators
            .Select(validator => Transaction.Create(
                nonce++,
                GenesisProposer,
                default,
                actions: new IAction[]
                    {
                        new Initialize
                        {
                            Validators = TestUtils.Validators,
                            States = ImmutableDictionary.Create<Address, IValue>(),
                        },
                    }.ToBytecodes(),
                timestamp: DateTimeOffset.UtcNow))
            .OrderBy(tx => tx.Id)
            .ToImmutableList();
        var genesis = BlockChain.ProposeGenesisBlock(transactions: [.. txs]);
        var chain = BlockChain.Create(
            policy,
            new VolatileStagePolicy(),
            store,
            stateStore,
            genesis,
            actionEvaluator);
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
        var tx1 = Transaction.Create(
            0,
            new PrivateKey(),
            genesisBlock.Hash,
            actions1.ToBytecodes());

        chain.StageTransaction(tx1);
        Block block1 = chain.ProposeBlock(new PrivateKey());
        chain.Append(block1, CreateBlockCommit(block1));
        var result = (BattleResult)chain
            .GetNextWorldState()
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
        var tx2 = Transaction.Create(
            0,
            new PrivateKey(),
            genesisBlock.Hash,
            actions2.ToBytecodes());

        chain.StageTransaction(tx2);
        Block block2 = chain.ProposeBlock(
            new PrivateKey(), CreateBlockCommit(chain.Tip));
        chain.Append(block2, CreateBlockCommit(block2));

        result = (BattleResult)chain
            .GetNextWorldState()
            .GetAccount(ReservedAddresses.LegacyAccount)
            .GetValue(_fx.Address1);

        Assert.Contains("bow", result.UsedWeapons);

        var tx3 = Transaction.Create(
            0,
            new PrivateKey(),
            genesisBlock.Hash,
            new[]
            {
                new Attack
                {
                    Weapon = "sword",
                    Target = "orc",
                    TargetAddress = _fx.Address1,
                },
            }.ToBytecodes()
        );
        Block block3 = chain.ProposeBlock(
            new PrivateKey(), CreateBlockCommit(chain.Tip));
        chain.StageTransaction(tx3);
        chain.Append(block3, CreateBlockCommit(block3));
        result = (BattleResult)chain
            .GetNextWorldState()
            .GetAccount(ReservedAddresses.LegacyAccount)
            .GetValue(_fx.Address1);
    }

    [SkippableFact]
    public void ActionRenderersHaveDistinctContexts()
    {
        var policy = new NullBlockPolicy();
        var store = new MemoryStore();
        var stateStore = new TrieStateStore();
        var generatedRandomValueLogs = new List<int>();
        IActionRenderer[] renderers = Enumerable.Range(0, 2).Select(i =>
            new LoggedActionRenderer(
                new AnonymousActionRenderer
                {
                    ActionRenderer = (act, context, nextState) =>
                        // Consuming the random state through IRandom.Next() should not
                        // affect contexts passed to other action renderers.
                        generatedRandomValueLogs.Add(context.GetRandom().Next()),
                },
                Log.Logger.ForContext("RendererIndex", i)
            )
        ).ToArray();
        BlockChain blockChain = MakeBlockChain(
            policy,
            store,
            stateStore,
            renderers: renderers
        );
        var privateKey = new PrivateKey();
        var action = DumbAction.Create((default, string.Empty));
        var actions = new[] { action };
        blockChain.MakeTransaction(privateKey, actions);
        Block block = blockChain.ProposeBlock(new PrivateKey());

        generatedRandomValueLogs.Clear();
        Assert.Empty(generatedRandomValueLogs);
        blockChain.Append(block, CreateBlockCommit(block));
        Assert.Equal(2, generatedRandomValueLogs.Count);
        Assert.Equal(generatedRandomValueLogs[0], generatedRandomValueLogs[1]);
    }

    [SkippableFact]
    public void RenderActionsAfterBlockIsRendered()
    {
        var policy = new NullBlockPolicy();
        var store = new MemoryStore();
        var stateStore = new TrieStateStore();
        var recordingRenderer = new RecordingActionRenderer();
        var renderer = new LoggedActionRenderer(recordingRenderer, Log.Logger);
        BlockChain blockChain = MakeBlockChain(
            policy, store, stateStore, renderers: new[] { renderer });
        var privateKey = new PrivateKey();

        var action = DumbAction.Create((default, string.Empty));
        var actions = new[] { action };
        blockChain.MakeTransaction(privateKey, actions);
        recordingRenderer.ResetRecords();
        Block prevBlock = blockChain.Tip;
        Block block = blockChain.ProposeBlock(new PrivateKey());
        blockChain.Append(block, CreateBlockCommit(block));

        Assert.Equal(2, blockChain.Count);
        IReadOnlyList<RenderRecord.BlockEvent> blockLogs = recordingRenderer.BlockRecords;
        Assert.Equal(2, blockLogs.Count);
        IReadOnlyList<RenderRecord.ActionBase> actionLogs = recordingRenderer.ActionRecords;
        Assert.Single(actions);
        Assert.Equal(prevBlock, blockLogs[0].OldTip);
        Assert.Equal(block, blockLogs[0].NewTip);
        Assert.Equal(0, blockLogs[0].Index);
        Assert.Equal(1, actionLogs[0].Index);
        Assert.Equal(ModelSerializer.Serialize(action), actionLogs[0].Action);
        Assert.Equal(prevBlock, blockLogs[1].OldTip);
        Assert.Equal(block, blockLogs[1].NewTip);
        Assert.Equal(2, blockLogs[1].Index);
    }

    [SkippableFact]
    public void RenderActionsAfterAppendComplete()
    {
        var policy = new NullBlockPolicy();
        var store = new MemoryStore();
        var stateStore = new TrieStateStore();

        IActionRenderer renderer = new AnonymousActionRenderer
        {
            ActionRenderer = (a, __, nextState) =>
            {
                if (!(a is Dictionary dictionary &&
                      dictionary.TryGetValue((Text)"type_id", out IValue typeId) &&
                      typeId.Equals((Integer)2)))
                {
                    throw new ThrowException.SomeException("thrown by renderer");
                }
            },
        };
        renderer = new LoggedActionRenderer(renderer, Log.Logger);
        BlockChain blockChain = MakeBlockChain(
            policy, store, stateStore, renderers: new[] { renderer });
        var privateKey = new PrivateKey();

        var action = DumbAction.Create((default, string.Empty));
        var actions = new[] { action };
        blockChain.MakeTransaction(privateKey, actions);
        Block block = blockChain.ProposeBlock(new PrivateKey());

        ThrowException.SomeException e = Assert.Throws<ThrowException.SomeException>(
            () => blockChain.Append(block, CreateBlockCommit(block)));
        Assert.Equal("thrown by renderer", e.Message);
        Assert.Equal(2, blockChain.Count);
    }

    [SkippableFact]
    public void FindNextHashes()
    {
        var key = new PrivateKey();
        IReadOnlyList<BlockHash> hashes;

        hashes = _blockChain.FindNextHashes(new BlockLocator(_blockChain.Genesis.Hash));
        Assert.Single(hashes);
        Assert.Equal(_blockChain.Genesis.Hash, hashes.First());
        var block0 = _blockChain.Genesis;
        var block1 = _blockChain.ProposeBlock(key);
        _blockChain.Append(block1, CreateBlockCommit(block1));
        var block2 = _blockChain.ProposeBlock(
            key, lastCommit: CreateBlockCommit(_blockChain.Tip));
        _blockChain.Append(block2, CreateBlockCommit(block2));
        var block3 = _blockChain.ProposeBlock(
            key, lastCommit: CreateBlockCommit(_blockChain.Tip));
        _blockChain.Append(block3, CreateBlockCommit(block3));

        hashes = _blockChain.FindNextHashes(new BlockLocator(block0.Hash));
        Assert.Equal(new[] { block0.Hash, block1.Hash, block2.Hash, block3.Hash }, hashes);

        hashes = _blockChain.FindNextHashes(new BlockLocator(block1.Hash));
        Assert.Equal(new[] { block1.Hash, block2.Hash, block3.Hash }, hashes);

        hashes = _blockChain.FindNextHashes(new BlockLocator(block0.Hash), count: 2);
        Assert.Equal(new[] { block0.Hash, block1.Hash }, hashes);
    }

    [SkippableFact]
    public void DetectInvalidTxNonce()
    {
        var privateKey = new PrivateKey();
        var actions = new[] { DumbAction.Create((_fx.Address1, "foo")) };

        var genesis = _blockChain.Genesis;

        Transaction[] txsA =
        [
            _fx.MakeTransaction(actions, privateKey: privateKey),
        ];

        Block b1 = _blockChain.ProposeBlock(
            _fx.Proposer,
            txsA.ToImmutableSortedSet(),
            CreateBlockCommit(_blockChain.Tip),
            []);
        _blockChain.Append(b1, TestUtils.CreateBlockCommit(b1));

        Block b2 = _blockChain.ProposeBlock(
            _fx.Proposer,
            txsA.ToImmutableSortedSet(),
            CreateBlockCommit(_blockChain.Tip),
            []);
        Assert.Throws<InvalidOperationException>(() =>
            _blockChain.Append(b2, CreateBlockCommit(b2)));

        Transaction[] txsB =
        [
            _fx.MakeTransaction(
                actions,
                nonce: 1,
                privateKey: privateKey),
        ];
        b2 = _blockChain.ProposeBlock(
            _fx.Proposer,
            txsB.ToImmutableSortedSet(),
            CreateBlockCommit(_blockChain.Tip),
            []);
        _blockChain.Append(b2, CreateBlockCommit(b2));
    }

    [SkippableFact]
    public void GetBlockLocator()
    {
        var key = new PrivateKey();
        List<Block> blocks = new List<Block>();
        foreach (var i in Enumerable.Range(0, 10))
        {
            var block = _blockChain.ProposeBlock(
                key,
                lastCommit: CreateBlockCommit(_blockChain.Tip));
            _blockChain.Append(block, CreateBlockCommit(block));
            blocks.Add(block);
        }

        BlockLocator actual = _blockChain.GetBlockLocator();
        BlockLocator expected = new BlockLocator(blocks[9].Hash);

        Assert.Equal(expected, actual);
    }

    [SkippableFact]
    public void GetBlockCommit()
    {
        // Note: Getting BlockCommit from PoW block test is not present.
        // Requesting blockCommit of genesis block returns null.
        Assert.Null(_blockChain.GetBlockCommit(0));
        Assert.Null(_blockChain.GetBlockCommit(_blockChain.Genesis.Hash));

        // BlockCommit is put to store when block is appended.
        Block block1 = _blockChain.ProposeBlock(new PrivateKey());
        BlockCommit blockCommit1 = CreateBlockCommit(block1);
        _blockChain.Append(block1, blockCommit1);
        Assert.Equal(blockCommit1, _blockChain.GetBlockCommit(block1.Height));
        Assert.Equal(blockCommit1, _blockChain.GetBlockCommit(block1.Hash));

        // BlockCommit is retrieved from lastCommit.
        Block block2 = _blockChain.ProposeBlock(
            new PrivateKey(),
            lastCommit: CreateBlockCommit(_blockChain.Tip));
        BlockCommit blockCommit2 = CreateBlockCommit(block2);
        _blockChain.Append(block2, blockCommit2);

        // These are different due to timestamps on votes.
        Assert.NotEqual(blockCommit1, _blockChain.GetBlockCommit(block1.Height));
        Assert.Equal(block2.LastCommit, _blockChain.GetBlockCommit(block1.Height));
        Assert.Equal(block2.LastCommit, _blockChain.GetBlockCommit(block1.Hash));
    }

    [SkippableFact]
    public void CleanupBlockCommitStore()
    {
        BlockCommit blockCommit1 = CreateBlockCommit(
            new BlockHash(GetRandomBytes(BlockHash.Size)), 1, 0);
        BlockCommit blockCommit2 = CreateBlockCommit(
            new BlockHash(GetRandomBytes(BlockHash.Size)), 2, 0);
        BlockCommit blockCommit3 = CreateBlockCommit(
            new BlockHash(GetRandomBytes(BlockHash.Size)), 3, 0);

        _blockChain.Store.PutBlockCommit(blockCommit1);
        _blockChain.Store.PutBlockCommit(blockCommit2);
        _blockChain.Store.PutBlockCommit(blockCommit3);
        _blockChain.CleanupBlockCommitStore(blockCommit3.Height);

        Assert.Null(_blockChain.Store.GetBlockCommit(blockCommit1.BlockHash));
        Assert.Null(_blockChain.Store.GetBlockCommit(blockCommit2.BlockHash));
        Assert.Equal(blockCommit3, _blockChain.Store.GetBlockCommit(blockCommit3.BlockHash));
    }

    [SkippableFact]
    public void GetStatesOnCreatingBlockChain()
    {
        bool invoked = false;
        var policy = new NullPolicyForGetStatesOnCreatingBlockChain(
            c =>
            {
                // ReSharper disable AccessToModifiedClosure
                // The following method calls should not throw any exceptions:
                invoked = true;
                // ReSharper restore AccessToModifiedClosure
            });
        IStore store = new MemoryStore();
        var stateStore = new TrieStateStore();
        var actionEvaluator = new ActionEvaluator(
            stateStore,
            policy.PolicyActions);
        Block genesisWithTx = ProposeGenesisBlock(
            ProposeGenesis(
                GenesisProposer.PublicKey,
                [
                    Transaction.Create(
                        0,
                        new PrivateKey(),
                        default,
                        []),
                ]),
            GenesisProposer);
        var chain = BlockChain.Create(
            policy,
            new VolatileStagePolicy(),
            store,
            stateStore,
            genesisWithTx,
            actionEvaluator);
        Assert.False(invoked);
    }

    // This is a regression test for:
    // https://github.com/planetarium/libplanet/issues/189#issuecomment-482443607.
    [SkippableFact]
    public void GetStateOnlyDrillsDownUntilRequestedAddressesAreFound()
    {
        var policy = new NullBlockPolicy();
        var tracker = new StoreTracker(_fx.Store);
        var blockChainStates = new BlockChainStates(tracker, _fx.StateStore);
        var chain = new BlockChain(
            policy,
            new VolatileStagePolicy(),
            tracker,
            _fx.StateStore,
            _fx.GenesisBlock,
            blockChainStates,
            new ActionEvaluator(
                _fx.StateStore,
                policy.PolicyActions));

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
                Transaction.Create(0, privateKey, chain.Genesis.Hash, actions.ToBytecodes()),
            ];
            b = chain.ProposeBlock(
                _fx.Proposer,
                txs.ToImmutableSortedSet(),
                CreateBlockCommit(chain.Tip),
                []);
            chain.Append(b, CreateBlockCommit(b));
        }

        tracker.ClearLogs();
        int testingDepth = addresses.Length / 2;
        Address[] targetAddresses = Enumerable.Range(
            testingDepth,
            Math.Min(10, addresses.Length - testingDepth - 1)
        ).Select(i => addresses[i]).ToArray();

        Assert.All(
            targetAddresses.Select(
                targetAddress => chain
                    .GetNextWorldState()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(targetAddress)),
            Assert.NotNull);

        var callCount = tracker.Logs.Where(
            trackLog => trackLog.Method == "GetBlockStates"
        ).Select(trackLog => trackLog.Params).Count();
        Assert.True(testingDepth >= callCount);
    }

    [SkippableFact]
    public void GetStateReturnsEarlyForNonexistentAccount()
    {
        var policy = new NullBlockPolicy();
        var tracker = new StoreTracker(_fx.Store);
        var blockChainStates = new BlockChainStates(tracker, _fx.StateStore);
        var chain = new BlockChain(
            policy,
            new VolatileStagePolicy(),
            tracker,
            _fx.StateStore,
            _fx.GenesisBlock,
            blockChainStates,
            new ActionEvaluator(
                _fx.StateStore,
                policy.PolicyActions));

        Block b = chain.Genesis;
        for (int i = 0; i < 20; ++i)
        {
            b = chain.ProposeBlock(_fx.Proposer, CreateBlockCommit(chain.Tip));
            chain.Append(b, CreateBlockCommit(b));
        }

        tracker.ClearLogs();
        Address nonexistent = new PrivateKey().Address;
        var result = chain
            .GetNextWorldState()
            .GetAccount(ReservedAddresses.LegacyAccount)
            .GetValue(nonexistent);
        Assert.Null(result);
        var callCount = tracker.Logs.Where(
            trackLog => trackLog.Method == "GetBlockStates"
        ).Select(trackLog => trackLog.Params).Count();
        Assert.True(
            callCount <= 1,
            $"GetBlocksStates() was called {callCount} times"
        );
    }

    [SkippableFact]
    public void GetStateReturnsLatestStatesWhenMultipleAddresses()
    {
        var privateKeys = Enumerable.Range(1, 10).Select(_ => new PrivateKey()).ToList();
        var addresses = privateKeys.Select(key => key.Address).ToList();
        var policy = new NullBlockPolicy();
        var blockChainStates = new BlockChainStates(_fx.Store, _fx.StateStore);
        var chain = new BlockChain(
            policy,
            new VolatileStagePolicy(),
            _fx.Store,
            _fx.StateStore,
            _fx.GenesisBlock,
            blockChainStates,
            new ActionEvaluator(
                _fx.StateStore,
                policy.PolicyActions));

        Assert.All(
            addresses.Select(
                address => chain
                    .GetNextWorldState()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(address)),
            Assert.Null);
        foreach (var address in addresses)
        {
            Assert.Null(chain
                .GetNextWorldState()
                .GetAccount(ReservedAddresses.LegacyAccount)
                .GetValue(address));
        }

        var privateKeysAndAddresses10 = privateKeys.Zip(addresses, (k, a) => (k, a));
        foreach (var (key, address) in privateKeysAndAddresses10)
        {
            chain.MakeTransaction(key, new[] { DumbAction.Create((address, "1")) });
        }

        Block block1 = chain.ProposeBlock(
            privateKeys[0], lastCommit: CreateBlockCommit(chain.Tip));

        chain.Append(block1, CreateBlockCommit(block1));

        Assert.All(
            addresses.Select(
                address => chain
                    .GetNextWorldState()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(address)),
            v => Assert.Equal((Text)"1", v));
        foreach (var address in addresses)
        {
            Assert.Equal(
                (Text)"1",
                chain
                    .GetNextWorldState()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(address));
        }

        chain.MakeTransaction(privateKeys[0], new[] { DumbAction.Create((addresses[0], "2")) });
        Block block2 = chain.ProposeBlock(
            privateKeys[0], lastCommit: CreateBlockCommit(chain.Tip));
        chain.Append(block2, CreateBlockCommit(block2));
        Assert.Equal(
            (Text)"1,2",
            chain
                .GetNextWorldState()
                .GetAccount(ReservedAddresses.LegacyAccount)
                .GetValue(addresses[0]));
        Assert.All(
            addresses.Skip(1).Select(
                address => chain
                    .GetNextWorldState()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(address)),
            v => Assert.Equal((Text)"1", v)
        );
    }

    [SkippableFact]
    public void FindBranchPoint()
    {
        var key = new PrivateKey();
        Block b1 = _blockChain.ProposeBlock(key);
        _blockChain.Append(b1, CreateBlockCommit(b1));
        Block b2 = _blockChain.ProposeBlock(
            key, lastCommit: CreateBlockCommit(_blockChain.Tip));
        _blockChain.Append(b2, CreateBlockCommit(b2));
        Block b3 = _blockChain.ProposeBlock(
            key, lastCommit: CreateBlockCommit(_blockChain.Tip));
        _blockChain.Append(b3, CreateBlockCommit(b3));
        Block b4 = _blockChain.ProposeBlock(
            key, lastCommit: CreateBlockCommit(_blockChain.Tip));
        _blockChain.Append(b4, CreateBlockCommit(b4));

        Assert.Equal(b1.PreviousHash, _blockChain.Genesis.Hash);

        var emptyLocator = new BlockLocator(_blockChain.Genesis.Hash);
        var invalidLocator = new BlockLocator(
            new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)));
        var locator = new BlockLocator(b4.Hash);

        using (var emptyFx = new MemoryStoreFixture(_policy.PolicyActions))
        using (var forkFx = new MemoryStoreFixture(_policy.PolicyActions))
        {
            var emptyChain = BlockChain.Create(
                _blockChain.Policy,
                new VolatileStagePolicy(),
                emptyFx.Store,
                emptyFx.StateStore,
                emptyFx.GenesisBlock,
                new ActionEvaluator(
                    stateStore: emptyFx.StateStore,
                    _blockChain.Policy.PolicyActions));
            var fork = BlockChain.Create(
                _blockChain.Policy,
                new VolatileStagePolicy(),
                forkFx.Store,
                forkFx.StateStore,
                forkFx.GenesisBlock,
                new ActionEvaluator(
                    stateStore: forkFx.StateStore,
                    _blockChain.Policy.PolicyActions));
            fork.Append(b1, CreateBlockCommit(b1));
            fork.Append(b2, CreateBlockCommit(b2));
            Block b5 = fork.ProposeBlock(
                key, lastCommit: CreateBlockCommit(fork.Tip));
            fork.Append(b5, CreateBlockCommit(b5));

            // Testing emptyChain
            Assert.Equal(_blockChain.Genesis.Hash, emptyChain.FindBranchpoint(emptyLocator));
            Assert.Null(emptyChain.FindBranchpoint(invalidLocator));
            Assert.Null(emptyChain.FindBranchpoint(locator));

            // Testing _blockChain
            Assert.Equal(_blockChain.Genesis.Hash, _blockChain.FindBranchpoint(emptyLocator));
            Assert.Null(_blockChain.FindBranchpoint(invalidLocator));
            Assert.Equal(b4.Hash, _blockChain.FindBranchpoint(locator));

            // Testing fork
            Assert.Equal(_blockChain.Genesis.Hash, fork.FindBranchpoint(emptyLocator));
            Assert.Null(fork.FindBranchpoint(invalidLocator));
            Assert.Null(fork.FindBranchpoint(locator));
        }
    }

    [SkippableFact]
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

        Block b1 = _blockChain.ProposeBlock(
            _fx.Proposer,
            txsA.ToImmutableSortedSet(),
            CreateBlockCommit(_blockChain.Tip),
            []);
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

        foreach (var tx in _blockChain.StagePolicy.Iterate(_blockChain))
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

    [SkippableFact]
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

        _blockChain.MakeTransaction(privateKey, actions);
        Assert.Equal(3, _blockChain.GetNextTxNonce(address));

        _blockChain.MakeTransaction(privateKey, actions);
        Assert.Equal(4, _blockChain.GetNextTxNonce(address));
    }

    [SkippableFact]
    public void ValidateTxNonces()
    {
        var privateKey = new PrivateKey();
        var actions = new[] { DumbAction.Create((_fx.Address1, string.Empty)) };

        var genesis = _blockChain.Genesis;

        Block ProposeNext(
            Block block,
            ImmutableSortedSet<Transaction> txs
        ) =>
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

    [SkippableFact]
    public void MakeTransactionWithSystemAction()
    {
        var privateKey = new PrivateKey();
        Address address = privateKey.Address;
        var action = new Initialize
        {
            Validators = [Validator.Create(new PrivateKey().PublicKey, 1)],
            States = new Dictionary<Address, IValue>
            {
                [default] = (Text)"initial value",
            }.ToImmutableDictionary(),
        };

        _blockChain.MakeTransaction(privateKey, actions: new IAction[] { action });
        _blockChain.MakeTransaction(privateKey, actions: new IAction[] { action });

        List<Transaction> txs = _stagePolicy
            .Iterate(_blockChain)
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

    [SkippableFact]
    public void MakeTransactionWithCustomActions()
    {
        var privateKey = new PrivateKey();
        Address address = privateKey.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        _blockChain.MakeTransaction(privateKey, actions);
        _blockChain.MakeTransaction(privateKey, actions);

        List<Transaction> txs = _stagePolicy
            .Iterate(_blockChain)
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

    [SkippableFact]
    public async Task MakeTransactionConcurrency()
    {
        var privateKey = new PrivateKey();
        Address address = privateKey.Address;
        var actions = new[] { DumbAction.Create((address, "foo")) };

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => _blockChain.MakeTransaction(privateKey, actions)));

        await Task.WhenAll(tasks);

        var txIds = _blockChain.GetStagedTransactionIds();

        var nonces = txIds
            .Select(id => _stagePolicy.Get(_blockChain, id)
                ?? _blockChain.GetTransaction(id))
            .Select(tx => tx.Nonce)
            .OrderBy(nonce => nonce)
            .ToArray();

        Assert.Equal(
            nonces,
            [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
        );
    }

    [SkippableFact]
    public void BlockActionWithMultipleAddress()
    {
        var miner0 = _blockChain.Genesis.Proposer;
        var miner1 = new PrivateKey();
        var miner2 = new PrivateKey();
        var rewardRecordAddress = MinerReward.RewardRecordAddress;

        Block block1 = _blockChain.ProposeBlock(
            miner1, lastCommit: CreateBlockCommit(_blockChain.Tip));
        _blockChain.Append(block1, CreateBlockCommit(block1));
        Block block2 = _blockChain.ProposeBlock(
            miner1, lastCommit: CreateBlockCommit(_blockChain.Tip));
        _blockChain.Append(block2, CreateBlockCommit(block2));
        Block block3 = _blockChain.ProposeBlock(
            miner2, lastCommit: CreateBlockCommit(_blockChain.Tip));
        _blockChain.Append(block3, CreateBlockCommit(block3));

        var miner1state = (int)_blockChain
            .GetNextWorldState()
            .GetAccount(ReservedAddresses.LegacyAccount)
            .GetValue(miner1.Address);
        var miner2state = (int)_blockChain
            .GetNextWorldState()
            .GetAccount(ReservedAddresses.LegacyAccount)
            .GetValue(miner2.Address);
        var rewardState = (string)_blockChain
            .GetNextWorldState()
            .GetAccount(ReservedAddresses.LegacyAccount)
            .GetValue(rewardRecordAddress);

        Assert.Equal(2, miner1state);
        Assert.Equal(1, miner2state);

        Assert.Equal(
            $"{miner0},{miner1.Address},{miner1.Address},{miner2.Address}",
            rewardState
        );
    }

    /// <summary>
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
        MakeIncompleteBlockStates(IStore store, IStateStore stateStore)
    {
        List<int> presentIndices = new List<int>() { 4, 7 };
        List<Block> presentBlocks = new List<Block>();

        IBlockPolicy blockPolicy = new NullBlockPolicy();
        store = new StoreTracker(store);
        Guid chainId = Guid.NewGuid();
        var chainStates = new BlockChainStates(store, stateStore);
        var actionEvaluator = new ActionEvaluator(
            stateStore: stateStore,
            blockPolicy.PolicyActions);
        Block genesisBlock = ProposeGenesisBlock(
            ProposeGenesis(GenesisProposer.PublicKey),
            GenesisProposer);
        var chain = BlockChain.Create(
            blockPolicy,
            new VolatileStagePolicy(),
            store,
            stateStore,
            genesisBlock,
            renderers: null,
            blockChainStates: chainStates,
            actionEvaluator: actionEvaluator);
        var privateKey = new PrivateKey();
        Address signer = privateKey.Address;

        void BuildIndex(Guid id, Block block)
        {
            foreach (Transaction tx in block.Transactions)
            {
                store.IncreaseTxNonce(id, tx.Signer);
            }

            store.AppendIndex(id, block.Hash);
        }

        // Build a store with incomplete states
        Block b = chain.Genesis;
        World previousState = stateStore.GetWorld(default);
        const int accountsCount = 5;
        Address[] addresses = Enumerable.Repeat<object>(null, accountsCount)
            .Select(_ => new PrivateKey().Address)
            .ToArray();
        for (int i = 0; i < 2; ++i)
        {
            for (int j = 0; j < accountsCount; ++j)
            {
                int index = i * accountsCount + j;
                Transaction tx = Transaction.Create(
                    store.GetTxNonce(chain.Id, signer),
                    privateKey,
                    chain.Genesis.Hash,
                    new[] { DumbAction.Create((addresses[j], index.ToString())) }.ToBytecodes()
                );
                b = chain.EvaluateAndSign(
                    ProposeNext(
                        b,
                        [tx],
                        blockInterval: TimeSpan.FromSeconds(10),
                        proposer: GenesisProposer.PublicKey,
                        lastCommit: CreateBlockCommit(b)),
                    GenesisProposer);

                var evals = actionEvaluator.EvaluateBlock((RawBlock)b, previousState);
                var dirty = evals.Last().OutputWorld.Trie
                    .Diff(evals.First().InputWorld.Trie)
                    .ToList();
                Assert.NotEmpty(dirty);
                store.PutBlock(b);
                BuildIndex(chain.Id, b);
                Assert.Equal(b, chain[b.Hash]);
                if (presentIndices.Contains((int)b.Height))
                {
                    presentBlocks.Add(b);
                }
            }
        }

        IStateStore incompleteStateStore = new TrieStateStore();
        ((TrieStateStore)stateStore).CopyStates(
            ImmutableHashSet<HashDigest<SHA256>>.Empty
                .Add(presentBlocks[0].StateRootHash)
                .Add(presentBlocks[1].StateRootHash),
            (TrieStateStore)incompleteStateStore);

        chain = new BlockChain(
            blockPolicy,
            new VolatileStagePolicy(),
            store,
            incompleteStateStore,
            genesisBlock,
            renderers: null,
            blockChainStates: chainStates,
            actionEvaluator: actionEvaluator);

        return (signer, addresses, chain);
    }

    /// <summary>
    /// Configures the store fixture that every test in this class depends on.
    /// Subclasses should override this.
    /// </summary>
    /// <param name="policyActions">The policy block actions to use.</param>
    /// <returns>The store fixture that every test in this class depends on.</returns>
    protected virtual StoreFixture GetStoreFixture(
        PolicyActions policyActions = null)
        => new MemoryStoreFixture(policyActions);

    private (Address[], Transaction[]) MakeFixturesForAppendTests(
        PrivateKey privateKey = null,
        DateTimeOffset epoch = default,
        PrivateKey[] keys = null
    )
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

    [SkippableFact]
    private void TipChanged()
    {
        var genesis = _blockChain.Genesis;

        _renderer.ResetRecords();

        Assert.Empty(_renderer.BlockRecords);
        Block block = _blockChain.ProposeBlock(new PrivateKey());
        _blockChain.Append(block, CreateBlockCommit(block));
        IReadOnlyList<RenderRecord.BlockEvent> records = _renderer.BlockRecords;
        Assert.Equal(2, records.Count);
        foreach (RenderRecord.BlockEvent record in records)
        {
            Assert.Equal(genesis, record.OldTip);
            Assert.Equal(block, record.NewTip);
            Assert.Equal(1, record.NewTip.Height);
        }

        _renderer.ResetRecords();
        Assert.Throws<InvalidOperationException>(
            () => _blockChain.Append(block, CreateBlockCommit(block)));
        Assert.Empty(_renderer.BlockRecords);
    }

    [SkippableFact]
    private void CreateWithGenesisBlock()
    {
        var storeFixture = new MemoryStoreFixture();
        var policy = new NullBlockPolicy();

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
                States = ImmutableDictionary.Create<Address, IValue>(),
                Validators = ImmutableSortedSet.Create(
                    [
                        Validator.Create(validatorPrivKey.PublicKey, BigInteger.One),
                    ]
                ),
            },
        };

        var customActions =
            addresses
                .Select((address, index) => DumbAction.Create((address, index.ToString())))
                .ToArray();

        var systemTxs = systemActions
            .Select((systemAction, i) => Transaction.Create(
                nonce: i,
                privateKey: privateKey,
                genesisHash: default,
                actions: new [] { systemAction }.ToBytecodes()))
            .ToArray();
        var customTxs = new[]
        {
            Transaction.Create(
                unsignedTx: new UnsignedTx
                {
                    Invoice = new TxInvoice
                    {
                        UpdatedAddresses = [.. addresses],
                        Timestamp = DateTimeOffset.UtcNow,
                        Actions = customActions.ToBytecodes(),
                        MaxGasPrice = default,
                    },
                    SigningMetadata = new TxSigningMetadata
                    {
                        Signer = privateKey.Address,
                        Nonce = systemTxs.Length,
                    },
                },
                privateKey: privateKey),
        };
        var txs = systemTxs.Concat(customTxs).ToImmutableList();
        var blockChainStates = new BlockChainStates(
            storeFixture.Store, storeFixture.StateStore);
        var actionEvaluator = new ActionEvaluator(
            storeFixture.StateStore,
            policy.PolicyActions);
        BlockChain blockChain = BlockChain.Create(
            policy,
            new VolatileStagePolicy(),
            storeFixture.Store,
            storeFixture.StateStore,
            BlockChain.ProposeGenesisBlock(
                privateKey: privateKey,
                transactions: [.. txs]),
            actionEvaluator);

        var validator = blockChain
            .GetNextWorldState()
            .GetValidatorSet()[0];
        Assert.Equal(validatorPrivKey.PublicKey, validator.PublicKey);
        Assert.Equal(BigInteger.One, validator.Power);
        // Assert.Equal(
        //     addresses,
        //     blockChain.Genesis.Transactions.Single(
        //         tx => tx.Actions.All(a => !Registry.IsSystemAction(a))).UpdatedAddresses);

        var states = addresses
            .Select(address => blockChain
                .GetNextWorldState()
                .GetAccount(ReservedAddresses.LegacyAccount)
                .GetValue(address))
            .ToArray();
        for (int i = 0; i < states.Length; ++i)
        {
            Assert.Equal((Text)states[i], i.ToString());
        }
    }

    [SkippableFact]
    private void ConstructWithUnexpectedGenesisBlock()
    {
        var policy = new NullBlockPolicy();
        var stagePolicy = new VolatileStagePolicy();
        IStore store = new MemoryStore();
        var stateStore = new TrieStateStore();
        var blockChainStates = new BlockChainStates(store, stateStore);
        var actionEvaluator = new ActionEvaluator(
            stateStore,
            policy.PolicyActions);
        var genesisBlockA = BlockChain.ProposeGenesisBlock();
        var genesisBlockB = BlockChain.ProposeGenesisBlock();

        var blockChain = BlockChain.Create(
            policy,
            stagePolicy,
            store,
            stateStore,
            genesisBlockA,
            actionEvaluator);

        Assert.Throws<InvalidOperationException>(() =>
        {
            var blockchain = new BlockChain(
                policy,
                stagePolicy,
                store,
                stateStore,
                genesisBlockB,
                blockChainStates,
                actionEvaluator);
        });
    }

    [SkippableFact]
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
        Assert.Single(_blockChain.GetStagedTransactionIds());
        Assert.Single(_blockChain.StagePolicy.Iterate(_blockChain, filtered: true));
        Assert.Equal(4, _blockChain.StagePolicy.Iterate(_blockChain, filtered: false).Count());
    }

    [SkippableFact]
    private void CheckIfTxPolicyExceptionHasInnerException()
    {
        var policy = new NullPolicyButTxPolicyAlwaysThrows(
            x =>
            {
                // ReSharper disable AccessToModifiedClosure
                // The following method calls should not throw any exceptions:
                x?.GetNextWorldState()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(default(Address));
                x?.GetNextWorldState()
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetValue(default);
                // ReSharper restore AccessToModifiedClosure
            });
        IStore store = new MemoryStore();
        var stateStore = new TrieStateStore();
        var genesisTx = Transaction.Create(
            0,
            new PrivateKey(),
            default,
            actions: []);
        var actionEvaluator = new ActionEvaluator(
            stateStore,
            policy.PolicyActions);
        var genesisWithTx = ProposeGenesisBlock(
            ProposeGenesis(GenesisProposer.PublicKey, [genesisTx]),
            privateKey: GenesisProposer);

        var chain = BlockChain.Create(
            policy,
            new VolatileStagePolicy(),
            store,
            stateStore,
            genesisWithTx,
            actionEvaluator);

        var blockTx = Transaction.Create(
            0,
            new PrivateKey(),
            default,
            Array.Empty<DumbAction>().ToBytecodes());
        var nextStateRootHash = chain.GetNextStateRootHash(genesisWithTx.Hash);
        var block = ProposeNextBlock(
            previousBlock: chain.Genesis,
            proposer: GenesisProposer,
            txs: [blockTx],
            stateRootHash: (HashDigest<SHA256>)nextStateRootHash);

        var e = Assert.Throws<InvalidOperationException>(
            () => chain.Append(block, CreateBlockCommit(block)));
        Assert.NotNull(e.InnerException);
    }

    [SkippableFact]
    private void ValidateNextBlockCommitOnValidatorSetChange()
    {
        var storeFixture = new MemoryStoreFixture();
        var policy = new NullBlockPolicy();

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
                pk => Validator.Create(pk.PublicKey, BigInteger.One)
            ).ToImmutableSortedSet();
        var systemActions = new[]
        {
            new Initialize
            {
                Validators = initialValidatorSet,
                States = ImmutableDictionary.Create<Address, IValue>(),
            },
        };
        var privateKey = new PrivateKey();
        var txs = systemActions
            .Select((systemAction, i) => Transaction.Create(
                nonce: i,
                privateKey: privateKey,
                genesisHash: default,
                actions: new IAction[] { systemAction }.ToBytecodes()))
            .ToImmutableList();

        var actionEvaluator = new ActionEvaluator(
            storeFixture.StateStore,
            policy.PolicyActions);
        Block genesis = BlockChain.ProposeGenesisBlock(
            privateKey: privateKey,
            transactions: [.. txs]);
        BlockChain blockChain = BlockChain.Create(
            policy,
            new VolatileStagePolicy(),
            storeFixture.Store,
            storeFixture.StateStore,
            genesis,
            actionEvaluator);

        blockChain.MakeTransaction(
            new PrivateKey(),
            new[]
            {
                new SetValidator
                {
                    Validator = Validator.Create(newValidatorPrivateKey.PublicKey),
                },
            }
        );
        var newBlock = blockChain.ProposeBlock(new PrivateKey());
        var newBlockCommit = new BlockCommit
        {
            Height = newBlock.Height,
            Round = 0,
            BlockHash = newBlock.Hash,
            Votes = [.. ValidatorPrivateKeys.Select(
                pk => new VoteMetadata
                {
                    Height = newBlock.Height,
                    Round = 0,
                    BlockHash = newBlock.Hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = pk.PublicKey,
                    ValidatorPower = TestUtils.Validators.GetValidator(pk.PublicKey).Power,
                    Flag = VoteFlag.PreCommit,
                }.Sign(pk))
            .OrderBy(vote => vote.ValidatorPublicKey.Address)],
        };
        blockChain.Append(newBlock, newBlockCommit);

        blockChain.MakeTransaction(
            new PrivateKey(),
            new[]
            {
                new SetValidator
                {
                    Validator = Validator.Create(new PrivateKey().PublicKey),
                },
            }
        );
        var nextBlock = blockChain.ProposeBlock(
            new PrivateKey(), lastCommit: newBlockCommit);
        var nextBlockCommit = new BlockCommit
        {
            Height = nextBlock.Height,
            Round = 0,
            BlockHash = nextBlock.Hash,
            Votes = [.. Enumerable.Range(0, newValidators.Length)
                .Select(
                    index => new VoteMetadata
                    {
                        Height = nextBlock.Height,
                        Round = 0,
                        BlockHash = nextBlock.Hash,
                        Timestamp = DateTimeOffset.UtcNow,
                        ValidatorPublicKey = newValidators[index].PublicKey,
                        ValidatorPower = newValidatorPowers[index],
                        Flag = VoteFlag.PreCommit,
                    }.Sign(newValidators[index]))
                .OrderBy(vote => vote.ValidatorPublicKey.Address)],
        };
        blockChain.Append(nextBlock, nextBlockCommit);

        blockChain.MakeTransaction(
            new PrivateKey(),
            new[]
            {
                new SetValidator
                {
                    Validator = Validator.Create(new PrivateKey().PublicKey),
                },
            }
        );
        var invalidCommitBlock = blockChain.ProposeBlock(
            new PrivateKey(), lastCommit: nextBlockCommit);

        Assert.Throws<InvalidOperationException>(
            () => blockChain.Append(
                invalidCommitBlock,
                new BlockCommit
                {
                    Height = invalidCommitBlock.Height,
                    Round = 0,
                    BlockHash = invalidCommitBlock.Hash,
                    Votes = [.. Enumerable.Range(0, newValidators.Length)
                        .Select(
                            index => new VoteMetadata
                            {
                                Height = invalidCommitBlock.Height,
                                Round = 0,
                                BlockHash = invalidCommitBlock.Hash,
                                Timestamp = DateTimeOffset.UtcNow,
                                ValidatorPublicKey = newValidators[index].PublicKey,
                                ValidatorPower = newValidatorPowers[index],
                                Flag = VoteFlag.PreCommit,
                            }.Sign(newValidators[index]))],
                }));

        Assert.Equal(
            blockChain
                .GetNextWorldState(0L)
                .GetValidatorSet(),
            [.. ValidatorPrivateKeys.Select(pk => Validator.Create(pk.PublicKey, BigInteger.One))]);

        Assert.Equal(
            blockChain
                .GetNextWorldState(1L)
                .GetValidatorSet(),
            [.. newValidators.Select(pk => Validator.Create(pk.PublicKey, BigInteger.One))]);
    }

    private class
        NullPolicyButTxPolicyAlwaysThrows : NullPolicyForGetStatesOnCreatingBlockChain
    {
        public NullPolicyButTxPolicyAlwaysThrows(
            Action<BlockChain> hook)
            : base(hook)
        {
        }

        public override InvalidOperationException ValidateNextBlockTx(
            BlockChain blockChain,
            Transaction transaction
        )
        {
            _hook(blockChain);
            return new InvalidOperationException("Test Message");
        }
    }

    private class NullPolicyForGetStatesOnCreatingBlockChain : NullBlockPolicy
    {
        protected readonly Action<BlockChain> _hook;

        public NullPolicyForGetStatesOnCreatingBlockChain(
            Action<BlockChain> hook
        )
        {
            _hook = hook;
        }

        public override InvalidOperationException ValidateNextBlockTx(
            BlockChain blockChain,
            Transaction transaction
        )
        {
            _hook(blockChain);
            return base.ValidateNextBlockTx(blockChain, transaction);
        }

        public override Exception ValidateNextBlock(
            BlockChain blocks,
            Block nextBlock
        )
        {
            _hook(blocks);
            return base.ValidateNextBlock(blocks, nextBlock);
        }
    }
}
