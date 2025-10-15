using Libplanet.Types;
using Microsoft.Extensions.Logging;

namespace Libplanet.Node.Services;

internal sealed class BlockchainService : IBlockchainService
{
    public BlockchainService(
        RepositoryService repositoryService,
        ILoggerFactory loggerFactory)
    {
        Blockchain = new Blockchain(
            repositoryService.Repository,
            new BlockchainOptions
            {
                Logger = loggerFactory.CreateLogger<Blockchain>(),
            });
    }

    public Blockchain Blockchain { get; }

    public Block Tip => Blockchain.Tip;

    public Block GetBlock(BlockHash blockHash) => Blockchain.Blocks[blockHash];

    public Block GetBlock(int height) => Blockchain.Blocks[height];
}
