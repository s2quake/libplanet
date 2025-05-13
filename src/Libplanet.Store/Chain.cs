using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class Chain(Guid chainId, IDatabase database)
{
    public BlockHashStore BlockHashes { get; } = new BlockHashStore(chainId, database);

    public NonceStore Nonces { get; } = new NonceStore(chainId, database);

    public Guid Id { get; } = chainId;

    public BlockCommit BlockCommit { get; set; } = BlockCommit.Empty;

    public int Height { get; set; } = -1;
}
