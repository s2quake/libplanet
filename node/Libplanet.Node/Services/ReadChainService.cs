using Libplanet.Types;

namespace Libplanet.Node.Services;

internal sealed class ReadChainService(IBlockChainService blockChainService) : IReadChainService
{
    public Block Tip => blockChainService.BlockChain.Tip;

    public Block GetBlock(BlockHash hash) => blockChainService.BlockChain.Blocks[hash];

    public Block GetBlock(int height) => blockChainService.BlockChain.Blocks[height];
}
