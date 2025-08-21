using System.Diagnostics;
using System.Text.Json;
using Libplanet.State;
using Libplanet.Node.Options;
using Libplanet.Serialization;
using Libplanet.Data;
using Libplanet.Types;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class BlockChainService(
    IOptions<GenesisOptions> genesisOptions,
    IRepositoryService storeService,
    IActionService actionService) : IBlockChainService
{
    private readonly Blockchain _blockChain = CreateBlockChain(
        actionService: actionService,
        genesisOptions: genesisOptions.Value,
        repository: storeService.Repository);

    public Blockchain BlockChain => _blockChain;

    private static Blockchain CreateBlockChain(
        IActionService actionService,
        GenesisOptions genesisOptions,
        Repository repository)
    {
        var genesisBlock = CreateGenesisBlock(genesisOptions, actionService, repository);
        var options = new BlockchainOptions
        {
            SystemActions = actionService.PolicyActions,
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

        return new Blockchain(new Repository(), options);
    }

    private static Block CreateGenesisBlock(
        GenesisOptions genesisOptions,
        IActionService actionService,
        Repository repository)
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
                repository);
        }

        if (genesisOptions.GenesisKey != string.Empty)
        {
            var genesisKey = PrivateKey.Parse(genesisOptions.GenesisKey);
            var genesisSigner = genesisKey.AsSigner();
            var validatorAddresses = genesisOptions.Validators.Select(Address.Parse).ToArray();
            var actions = actionService.GetGenesisActions(
                genesisAddress: genesisKey.Address,
                validators: validatorAddresses);
            return new BlockBuilder
            {
                Transactions =
                [
                    new InitialTransactionBuilder
                    {
                        Actions = actions,
                    }.Create(genesisSigner),
                ],
            }.Create(genesisSigner, repository);
        }

        throw new UnreachableException("Genesis block path is not set.");
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
        PrivateKey genesisKey, byte[] config, Repository repository)
    {
        var accountStates = JsonSerializer.Deserialize<AccountState[]>(config)
            ?? throw new InvalidOperationException(
                "Failed to deserialize genesis configuration. Ensure it is a valid JSON array of AccountState.");

        var trie = repository.States.GetTrie(repository.StateRootHash);
        var world = new World(trie, repository.States);

        foreach (var accountState in accountStates)
        {
            var name = accountState.Name;
            var account = world.GetAccount(name);

            foreach (var value in accountState.Values)
            {
                account = account.SetValue(value.Key, value.Value);
            }

            world = world.SetAccount(name, account);
        }

        world = world.Commit();
        repository.StateRootHash = world.Hash;

        return new BlockBuilder
        {
        }.Create(genesisKey.AsSigner(), repository);
    }
}
