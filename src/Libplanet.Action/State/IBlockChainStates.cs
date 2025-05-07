using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Action.State;

public interface IBlockChainStates
{
    World GetWorld(BlockHash blockHash);

    World GetWorld(HashDigest<SHA256> stateRootHash);
}
