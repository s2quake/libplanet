using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    internal Block EvaluateAndSign(RawBlock rawBlock, PrivateKey privateKey)
    {
        if (rawBlock.Header.Height < 1)
        {
            throw new ArgumentException(
                $"Given {nameof(rawBlock)} must have block height " +
                $"higher than 0");
        }
        else
        {
            var prevBlock = Blocks[rawBlock.Header.PreviousHash];
            var stateRootHash = GetNextStateRootHash(prevBlock.BlockHash);
            return rawBlock.Sign(privateKey, stateRootHash);
        }
    }
}
