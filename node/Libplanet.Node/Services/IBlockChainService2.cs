using Libplanet.Types;

namespace Libplanet.Node.Services;

public interface IBlockchainService
{
    Block Tip { get; }

    Block GetBlock(BlockHash blockHash);

    Block GetBlock(int height);
}
