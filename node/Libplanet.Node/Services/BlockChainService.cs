using System.Diagnostics;
using Libplanet.Node.Options;
using Libplanet.Serialization;
using Libplanet.Types;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Libplanet.Node.Services;

internal sealed class BlockchainService : IBlockchainService
{
    public BlockchainService(
        IOptions<GenesisOptions> genesisOptions,
        RepositoryService repositoryService,
        ILoggerFactory loggerFactory)
    {
        Blockchain = new Blockchain(
            CreateGenesisBlock(genesisOptions.Value),
            repositoryService.Repository,
            new BlockchainOptions
            {
                Logger = loggerFactory.CreateLogger<Blockchain>(),
            });
    }

    public Blockchain Blockchain { get; }

    public Block Tip => Blockchain.Tip;

    private static Block CreateGenesisBlock(GenesisOptions genesisOptions)
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

        if (genesisOptions.GenesisKey != string.Empty)
        {
            var genesisKey = PrivateKey.Parse(genesisOptions.GenesisKey);
            var genesisSigner = genesisKey.AsSigner();
            var validatorAddresses = genesisOptions.Validators.Select(Address.Parse).ToArray();
            return new GenesisBlockBuilder
            {
                Validators = [.. validatorAddresses.Select(a => new Validator { Address = a })],
            }.Create(genesisSigner);
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

    public Block GetBlock(BlockHash hash)
    {
        return Blockchain.Blocks[hash];
    }

    public Block GetBlock(int height)
    {
        return Blockchain.Blocks[height];
    }
}
