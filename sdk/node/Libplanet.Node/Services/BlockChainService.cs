using System.Diagnostics;
using System.Text.Json;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Blockchain;
using Libplanet.Node.Options;
using Libplanet.Serialization;
using Libplanet.Store;
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
        store: storeService.Repository);

    public BlockChain BlockChain => _blockChain;

    private static BlockChain CreateBlockChain(
        IActionService actionService,
        GenesisOptions genesisOptions,
        Repository store)
    {
        var genesisBlock = CreateGenesisBlock(genesisOptions, actionService, store.StateStore);
        var options = new BlockChainOptions
        {
            PolicyActions = actionService.PolicyActions,
            BlockInterval = TimeSpan.FromSeconds(8),
            BlockOptions = new BlockOptions
            {
                MaxTransactionsBytes = long.MaxValue,
                MinTransactionsPerBlock = 0,
                MaxTransactionsPerBlock = int.MaxValue,
                MaxTransactionsPerSignerPerBlock = int.MaxValue,
            },
        };

        // if (store.ChainId == Guid.Empty)
        // {
        //     return new BlockChain(genesisBlock, options);
        // }

        return new BlockChain(new Repository(), options);
    }

    private static Block CreateGenesisBlock(
        GenesisOptions genesisOptions,
        IActionService actionService,
        TrieStateStore stateStore)
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
            var validatorAddresses = genesisOptions.Validators.Select(Address.Parse).ToArray();
            var actions = actionService.GetGenesisActions(
                genesisAddress: genesisKey.Address,
                validators: validatorAddresses);
            return CreateGenesisBlock(genesisKey, actions);
        }

        throw new UnreachableException("Genesis block path is not set.");
    }

    private static Block CreateGenesisBlock(
        PrivateKey genesisKey, IAction[] actions)
    {
        var nonce = 0L;
        var transaction = new TransactionMetadata
        {
            Nonce = nonce,
            Signer = genesisKey.Address,
            GenesisHash = default,
            Actions = actions.ToBytecodes(),
            Timestamp = DateTimeOffset.MinValue,
        }.Sign(genesisKey);
        return BlockChain.ProposeGenesisBlock(
            proposer: genesisKey,
            previousStateRootHash: default,
            transactions: [transaction]);
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
        TrieStateStore stateStore)
    {
        Dictionary<string, Dictionary<string, object>>? data =
            JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(config);
        if (data == null || data.Count == 0)
        {
            return BlockChain.ProposeGenesisBlock(
                proposer: genesisKey,
                previousStateRootHash: default,
                transactions: []);
        }

        var nullTrie = stateStore.GetStateRoot(default);
        World world = new World
        {
            Trie = nullTrie,
            StateStore = stateStore,
        };

        foreach (var accountKv in data)
        {
            var key = accountKv.Key;
            Account account = world.GetAccount(key);

            foreach (var stateKv in accountKv.Value)
            {
                account = account.SetValue(stateKv.Key, stateKv.Value);
            }

            world = world.SetAccount(key, account);
        }

        var worldTrie = world.Trie;
        foreach (var (name, account) in world.Delta)
        {
            var accountTrie = stateStore.Commit(account.Trie);
            worldTrie = worldTrie.Set(
                name,
                accountTrie.Hash.Bytes);
        }

        worldTrie = stateStore.Commit(worldTrie);
        return BlockChain.ProposeGenesisBlock(
            proposer: genesisKey,
            previousStateRootHash: worldTrie.Hash,
            transactions: []);
    }
}
