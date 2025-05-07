using System.Diagnostics;
using System.Text.Json;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Node.Options;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class BlockChainService(
    IOptions<GenesisOptions> genesisOptions,
    IStoreService storeService,
    IActionService actionService,
    PolicyService policyService) : IBlockChainService
{
    private readonly BlockChain _blockChain = CreateBlockChain(
        actionService: actionService,
        genesisOptions: genesisOptions.Value,
        store: storeService.Store,
        stateStore: storeService.StateStore);

    public BlockChain BlockChain => _blockChain;

    private static BlockChain CreateBlockChain(
        IActionService actionService,
        GenesisOptions genesisOptions,
        IStore store,
        IStateStore stateStore)
    {
        var actionEvaluator = new ActionEvaluator(
            stateStore,
            actionService.PolicyActions);
        var genesisBlock = CreateGenesisBlock(genesisOptions, actionService, stateStore);
        var policy = BlockPolicy.Empty with
        {
            PolicyActions = actionService.PolicyActions,
            BlockInterval = TimeSpan.FromSeconds(8),
            MaxTransactionsBytes = long.MaxValue,
            MinTransactionsPerBlock = 0,
            MaxTransactionsPerBlock = int.MaxValue,
            MaxTransactionsPerSignerPerBlock = int.MaxValue,
        };

        var blockChainStates = new BlockChainStates(store, stateStore);
        if (store.GetCanonicalChainId() is null)
        {
            return BlockChain.Create(
                policy: policy,
                store: store,
                stateStore: stateStore,
                genesisBlock: genesisBlock,
                actionEvaluator: actionEvaluator,
                blockChainStates: blockChainStates);
        }

        return new BlockChain(
            policy: policy,
            store: store,
            stateStore: stateStore,
            genesisBlock: genesisBlock,
            blockChainStates: blockChainStates,
            actionEvaluator: actionEvaluator);
    }

    private static Block CreateGenesisBlock(
        GenesisOptions genesisOptions,
        IActionService actionService,
        IStateStore stateStore)
    {
        if (genesisOptions.GenesisBlockPath != string.Empty)
        {
            return genesisOptions.GenesisBlockPath switch
            {
                { } path when Uri.TryCreate(path, UriKind.Absolute, out var uri)
                    => LoadGenesisBlockFromUrl(uri),
                { } path => LoadGenesisBlock(path),
                _ => throw new NotSupportedException(),
            };
        }

        if (genesisOptions.GenesisConfigurationPath != string.Empty)
        {
            var raw = genesisOptions.GenesisConfigurationPath switch
            {
                { } path when Uri.TryCreate(path, UriKind.Absolute, out var uri)
                    => LoadConfigurationFromUri(uri),
                { } path => LoadConfigurationFromFilePath(path),
                _ => throw new NotSupportedException(),
            };

            return CreateGenesisBlockFromConfiguration(
                PrivateKey.Parse(genesisOptions.GenesisKey),
                raw,
                stateStore);
        }

        if (genesisOptions.GenesisKey != string.Empty)
        {
            var genesisKey = PrivateKey.Parse(genesisOptions.GenesisKey);
            var validatorKeys = genesisOptions.Validators.Select(PublicKey.Parse).ToArray();
            var actions = actionService.GetGenesisActions(
                genesisAddress: genesisKey.Address,
                validatorKeys: validatorKeys);
            return CreateGenesisBlock(genesisKey, actions);
        }

        throw new UnreachableException("Genesis block path is not set.");
    }

    private static Block CreateGenesisBlock(
        PrivateKey genesisKey, IAction[] actions)
    {
        var nonce = 0L;
        var transaction = Transaction.Create(
            nonce: nonce,
            privateKey: genesisKey,
            genesisHash: default,
            actions: actions.ToBytecodes(),
            timestamp: DateTimeOffset.MinValue);
        return BlockChain.ProposeGenesisBlock(
            proposer: genesisKey,
            transactions: [transaction],
            timestamp: DateTimeOffset.MinValue);
    }

    private static Block LoadGenesisBlock(string genesisBlockPath)
    {
        var rawBlock = File.ReadAllBytes(Path.GetFullPath(genesisBlockPath));
        return ModelSerializer.DeserializeFromBytes<Block>(rawBlock);
    }

    private static Block LoadGenesisBlockFromUrl(Uri genesisBlockUri)
    {
        using var client = new HttpClient();
        var rawBlock = client.GetByteArrayAsync(genesisBlockUri).Result;
        return ModelSerializer.DeserializeFromBytes<Block>(rawBlock);
    }

    private static byte[] LoadConfigurationFromFilePath(string configurationPath)
    {
        return File.ReadAllBytes(Path.GetFullPath(configurationPath));
    }

    private static byte[] LoadConfigurationFromUri(Uri configurationUri)
    {
        using var client = new HttpClient();
        return configurationUri.IsFile
            ? LoadConfigurationFromFilePath(configurationUri.AbsolutePath)
            : client.GetByteArrayAsync(configurationUri).Result;
    }

    private static Block CreateGenesisBlockFromConfiguration(
        PrivateKey genesisKey,
        byte[] config,
        IStateStore stateStore)
    {
        Dictionary<string, Dictionary<string, string>>? data =
            JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(config);
        if (data == null || data.Count == 0)
        {
            return BlockChain.ProposeGenesisBlock(
                proposer: genesisKey,
                timestamp: DateTimeOffset.MinValue);
        }

        var nullTrie = stateStore.GetStateRoot(default);
        World world = new World
        {
            Trie = nullTrie,
            StateStore = stateStore,
        };
        var codec = new Codec();

        foreach (var accountKv in data)
        {
            var key = Address.Parse(accountKv.Key);
            Account account = world.GetAccount(key);

            foreach (var stateKv in accountKv.Value)
            {
                account = account.SetValue(
                    Address.Parse(stateKv.Key),
                    codec.Decode(ByteUtility.ParseHex(stateKv.Value)));
            }

            world = world.SetAccount(key, account);
        }

        var worldTrie = world.Trie;
        foreach (var (address, account) in world.Delta)
        {
            var accountTrie = stateStore.Commit(account.Trie);
            worldTrie = worldTrie.Set(
                KeyConverters.ToStateKey(address),
                new Binary(accountTrie.Hash.Bytes));
        }

        worldTrie = stateStore.Commit(worldTrie);
        return BlockChain.ProposeGenesisBlock(
            proposer: genesisKey,
            stateRootHash: worldTrie.Hash,
            timestamp: DateTimeOffset.MinValue);
    }
}
