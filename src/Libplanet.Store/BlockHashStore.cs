using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockHashStore(Guid chainId, IDatabase database)
    : CollectionBase<int, BlockHash>(database.GetOrAdd(GetKey(chainId)))
{
    private int _genesisHeight;

    public int GenesisHeight
    {
        get => _genesisHeight;
        set
        {
            if (Count > 0)
            {
                throw new InvalidOperationException("Cannot set GenesisHeight after adding blocks.");
            }

            ArgumentOutOfRangeException.ThrowIfNegative(value);

            _genesisHeight = value;
        }
    }

    public int Height { get; private set; }

    public BlockHash this[Index index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return base[index.GetOffset(Height + 1)];
        }

        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            base[index.GetOffset(Height + 1)] = value;
        }
    }

    public void Add(Block block) => Add(block.Height, block.BlockHash);

    public IEnumerable<BlockHash> IterateHeights(int height = 0, int? limit = null)
    {
        if (IsDisposed)
        {
            yield break;
        }

        var begin = height + GenesisHeight;
        var end = checked(limit is { } l ? begin + l : int.MaxValue);
        for (var i = begin; i < end; i++)
        {
            if (TryGetValue(i, out var blockHash))
            {
                yield return blockHash;
            }
            else
            {
                break;
            }
        }
    }

    internal static string GetKey(Guid chainId) => $"{chainId}_block_hash";

    protected override void OnAddComplete(int key, BlockHash item)
    {
        base.OnAddComplete(key, item);
        Height = Math.Max(Height, key);
    }

    protected override void OnSetComplete(int key, BlockHash item)
    {
        base.OnSetComplete(key, item);
        Height = Math.Max(Height, key);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            database.Remove(GetKey(chainId));
        }

        base.Dispose(disposing);
    }

    protected override byte[] GetBytes(BlockHash value) => [.. value.Bytes];

    protected override int GetKey(KeyBytes keyBytes) => BitConverter.ToInt32([.. keyBytes.Bytes]);

    protected override KeyBytes GetKeyBytes(int key) => new(BitConverter.GetBytes(key));

    protected override BlockHash GetValue(byte[] bytes) => new(bytes);
}
