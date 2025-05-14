using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Store;

public sealed class Chain(Guid chainId, IDatabase database) : IDisposable, IHasKey<Guid>
{
    private bool _disposed;

    public BlockHashStore BlockHashes { get; } = new BlockHashStore(chainId, database);

    public NonceStore Nonces { get; } = new NonceStore(chainId, database);

    public Guid Id { get; } = chainId;

    public BlockCommit BlockCommit { get; set; } = BlockCommit.Empty;

    public int GenesisHeight
    {
        get => BlockHashes.GenesisHeight;
        set => BlockHashes.GenesisHeight = value;
    }

    public int Height => BlockHashes.Height;

    Guid IHasKey<Guid>.Key => Id;

    public long GetNonce(Address address) => Nonces.GetValueOrDefault(address);

    public long IncreaseNonce(Address address, long delta = 1L) => Nonces.Increase(address, delta);

    public void ForkFrom(Chain source) => ForkFrom(source, default);

    public void ForkFrom(Chain source, BlockHash branchPoint)
    {
        var genesisHash = source.BlockHashes.IterateHeights(0, 1).FirstOrDefault();
        if (genesisHash == default || branchPoint == genesisHash)
        {
            return;
        }

        for (var i = source.GenesisHeight; i <= source.Height; i++)
        {
            var blockHash = source.BlockHashes[i];
            BlockHashes[i] = blockHash;
            if (blockHash.Equals(branchPoint))
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            BlockHashes.Dispose();
            Nonces.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
