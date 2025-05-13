using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class Chain(Store store, Guid chainId)
{
    public BlockCollection Blocks { get; } = new BlockCollection(store, chainId);

    public BlockHashStore BlockHashes { get; } = store.GetBlockHashes(chainId);

    public NonceCollection Nonces { get; } = store.GetNonceCollection(chainId);

    public Guid Id { get; } = chainId;

    public BlockCommit BlockCommit { get; set; } = BlockCommit.Empty;

    public int Height { get; set; } = -1;
}
