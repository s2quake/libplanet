using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Types.Blocks;

namespace Libplanet.Action.State;

public interface IBlockChainStates
{
    World GetWorld(BlockHash offset);

    World GetWorld(HashDigest<SHA256> stateRootHash);
}
