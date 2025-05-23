using System.Security.Cryptography;
using Libplanet.Action;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    public World GetWorld() => GetWorld(Tip.BlockHash);

    public World GetWorld(int height) => GetWorld(Blocks[height].BlockHash);

    public World GetWorld(BlockHash blockHash)
    {
        var stateRootHash = _repository.StateRootHashStore[blockHash];
        return new World(_repository.StateStore.GetStateRoot(stateRootHash), _repository.StateStore);
    }

    public World GetWorld(HashDigest<SHA256> stateRootHash)
    {
        return new World(_repository.StateStore.GetStateRoot(stateRootHash), _repository.StateStore);
    }
}
