using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Types.Blocks;

namespace Libplanet.Action.State;

public interface IBlockChainStates
{
    IWorldState GetWorldState(BlockHash offset);

    IWorldState GetWorldState(HashDigest<SHA256> stateRootHash);
}
