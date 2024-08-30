using Libplanet.Blockchain;
using Libplanet.Types.Blocks;

namespace Libplanet.Node.Services;

public interface IBlockChainService
{
    BlockChain BlockChain { get; }
}
