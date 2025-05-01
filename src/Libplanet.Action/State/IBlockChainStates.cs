using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Types.Blocks;

namespace Libplanet.Action.State;

public interface IBlockChainStates
{
    World GetWorldState(BlockHash offset);

    World GetWorldState(HashDigest<SHA256> stateRootHash);
}
