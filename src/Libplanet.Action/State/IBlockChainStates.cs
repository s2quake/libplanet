using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Types.Blocks;

namespace Libplanet.Action.State;

public interface IBlockChainStates
{
    IWorld GetWorldState(BlockHash offset);

    IWorld GetWorldState(HashDigest<SHA256> stateRootHash);
}
