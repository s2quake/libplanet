using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.Sys;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Node.Options;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Tx;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class BlockChainService : IBlockChainService, IActionRenderer
{
    private readonly SynchronizationContext _synchronizationContext;
    private readonly StoreOptions _storeOption;
    private readonly GenesisOptions _genesisOptions;
    private readonly ILogger<BlockChainService> _logger;
    private readonly BlockChain _blockChain;
    private readonly ConcurrentDictionary<TxId, ManualResetEvent> _eventByTxId = [];
    private readonly ConcurrentDictionary<IValue, Exception> _exceptionByAction = [];

    public BlockChainService(
        PolicyService policyService,
        IOptions<StoreOptions> storeOption,
        IOptions<GenesisOptions> genesisOptions,
        IEnumerable<IActionLoaderProvider> actionLoaderProviders,
        ILogger<BlockChainService> logger)
    {
        _synchronizationContext = SynchronizationContext.Current!;
        _storeOption = storeOption.Value;
        _genesisOptions = genesisOptions.Value;
        _logger = logger;
        _blockChain = CreateBlockChain(
            policyService.StagePolicy,
            [this],
            CollectActionLoaders(actionLoaderProviders));
    }

    public event EventHandler<BlockEventArgs>? BlockAppended;

    public GenesisOptions GenesisOptions => _genesisOptions;

    public BlockChain BlockChain => _blockChain;

    void IRenderer.RenderBlock(Block oldTip, Block newTip)
    {
    }

    void IActionRenderer.RenderAction(
        IValue action, ICommittedActionContext context, HashDigest<SHA256> nextState)
    {
    }

    void IActionRenderer.RenderActionError(
        IValue action, ICommittedActionContext context, Exception exception)
    {
        _exceptionByAction.AddOrUpdate(action, exception, (_, _) => exception);
    }

    void IActionRenderer.RenderBlockEnd(Block oldTip, Block newTip)
    {
        _synchronizationContext.Post(Action, state: null);

        void Action(object? state)
        {
            foreach (var transaction in newTip.Transactions)
            {
                if (_eventByTxId.TryGetValue(transaction.Id, out var manualResetEvent))
                {
                    manualResetEvent.Set();
                }
            }

            _logger.LogInformation("#{Height}: Block appended.", newTip.Index);
            BlockAppended?.Invoke(this, new(newTip));
        }
    }

    private static IActionLoader[] CollectActionLoaders(
        IEnumerable<IActionLoaderProvider> actionLoaderProviders)
    {
        var actionLoaderList
            = actionLoaderProviders.Select(item => item.GetActionLoader()).ToList();
        actionLoaderList.Add(new AssemblyActionLoader(typeof(AssemblyActionLoader).Assembly));
        return [.. actionLoaderList];
    }

    private BlockChain CreateBlockChain(
        IStagePolicy stagePolicy,
        IRenderer[] renderers,
        IActionLoader[] actionLoaders)
    {
        var genesisKey = PrivateKey.FromString(_genesisOptions.GenesisKey);
        var (store, stateStore) = CreateStore();
        var actionLoader = new AggregateTypedActionLoader(actionLoaders);
        var actionEvaluator = new ActionEvaluator(
            policyActionsRegistry: new(),
            stateStore,
            actionLoader);
        var validators = _genesisOptions.Validators.Select(PublicKey.FromHex)
                            .Select(item => new Validator(item, new BigInteger(1000)))
                            .ToArray();
        var validatorSet = new ValidatorSet(validators: [.. validators]);
        var nonce = 0L;
        IAction[] actions =
        [
            new Initialize(
                validatorSet: validatorSet,
                states: ImmutableDictionary.Create<Address, IValue>()),
        ];

        var transaction = Transaction.Create(
            nonce: nonce,
            privateKey: genesisKey,
            genesisHash: null,
            actions: [.. actions.Select(item => item.PlainValue)],
            timestamp: DateTimeOffset.MinValue);
        var transactions = ImmutableList.Create(transaction);
        var genesisBlock = BlockChain.ProposeGenesisBlock(
            privateKey: genesisKey,
            transactions: transactions,
            timestamp: DateTimeOffset.MinValue);
        var policy = new BlockPolicy(
            blockInterval: TimeSpan.FromSeconds(10),
            getMaxTransactionsPerBlock: _ => int.MaxValue,
            getMaxTransactionsBytes: _ => long.MaxValue);
        var blockChainStates = new BlockChainStates(store, stateStore);
        if (store.GetCanonicalChainId() is null)
        {
            return BlockChain.Create(
                policy: policy,
                stagePolicy: stagePolicy,
                store: store,
                stateStore: stateStore,
                genesisBlock: genesisBlock,
                actionEvaluator: actionEvaluator,
                renderers: renderers,
                blockChainStates: blockChainStates);
        }

        return new BlockChain(
            policy: policy,
            stagePolicy: stagePolicy,
            store: store,
            stateStore: stateStore,
            genesisBlock: genesisBlock,
            blockChainStates: blockChainStates,
            actionEvaluator: actionEvaluator,
            renderers: renderers);
    }

    private (IStore, IStateStore) CreateStore()
        => _storeOption.Type switch
        {
            StoreType.Disk => CreateRocksDBStore(),
            StoreType.Memory => CreateInMemoryStore(),
            _ => throw new NotSupportedException($"Unsupported store type: {_storeOption.Type}"),
        };

    private (MemoryStore, TrieStateStore) CreateInMemoryStore()
    {
        var store = new MemoryStore();
        var stateStore = new TrieStateStore(new MemoryKeyValueStore());
        return (store, stateStore);
    }

    private (RocksDBStore.RocksDBStore, TrieStateStore) CreateRocksDBStore()
    {
        var store = new RocksDBStore.RocksDBStore(_storeOption.StoreName);
        var stateStore = new TrieStateStore(new RocksDBKeyValueStore(_storeOption.StateStoreName));
        return (store, stateStore);
    }
}
