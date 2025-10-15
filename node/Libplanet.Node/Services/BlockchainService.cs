using Libplanet.Node.Options;
using Libplanet.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class BlockchainService : IBlockchainService
{
    public BlockchainService(
        IOptions<NodeOptions> nodeOptions,
        RepositoryService repositoryService,
        ILoggerFactory loggerFactory)
    {
        var repository = repositoryService.Repository;
        var options = new BlockchainOptions
        {
            Logger = loggerFactory.CreateLogger<Blockchain>(),
        };
        if (repositoryService.Type == RepositoryType.Memory)
        {
            var signer = PrivateKey.Parse(nodeOptions.Value.PrivateKey).AsSigner();
            var genesisBlock = new GenesisBlockBuilder
            {
                Validators = [new Validator { Address = signer.Address }],
            }.Create(signer);
            Blockchain = new Blockchain(genesisBlock, repository, options);
        }
        else
        {
            Blockchain = new Blockchain(repository, options);
        }
    }

    public Blockchain Blockchain { get; }

    public Block Tip => Blockchain.Tip;

    public Block GetBlock(BlockHash blockHash) => Blockchain.Blocks[blockHash];

    public Block GetBlock(int height) => Blockchain.Blocks[height];
}
