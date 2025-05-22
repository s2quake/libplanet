using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockHashStore(IDatabase database)
    : StoreBase<int, BlockHash>(database.GetOrAdd("block_hash"))
{
    public int GenesisHeight { get; internal set; }

    public int Height { get; internal set; }

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

    public IEnumerable<BlockHash> this[Range range]
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            var (offset, length) = range.GetOffsetAndLength(Height + 1);
            var start = offset;
            var end = offset + length;

            for (var i = start; i < end; i++)
            {
                if (TryGetValue(i, out var blockHash))
                {
                    yield return blockHash;
                }
            }
        }
    }

    public void Add(Block block) => Add(block.Height, block.BlockHash);

    // public IEnumerable<BlockHash> Skip(int height)
    // {
    //     if (IsDisposed)
    //     {
    //         yield break;
    //     }

    //     var begin = height + GenesisHeight;
    //     for (var i = begin; i < Count; i++)
    //     {
    //         if (TryGetValue(i, out var blockHash))
    //         {
    //             yield return blockHash;
    //         }
    //     }
    // }

    // public IEnumerable<BlockHash> Take(int height)
    // {
    //     if (IsDisposed)
    //     {
    //         yield break;
    //     }

    //     var begin = GenesisHeight;
    //     var end = checked(height + GenesisHeight);
    //     for (var i = begin; i < end; i++)
    //     {
    //         if (TryGetValue(i, out var blockHash))
    //         {
    //             yield return blockHash;
    //         }
    //         else
    //         {
    //             break;
    //         }
    //     }
    // }

    // public IEnumerable<BlockHash> IterateHeights(int height = 0, int? limit = null)
    // {
    //     if (IsDisposed)
    //     {
    //         yield break;
    //     }

    //     var begin = height + GenesisHeight;
    //     var end = checked(limit is { } l ? begin + l : int.MaxValue);
    //     for (var i = begin; i < end; i++)
    //     {
    //         if (TryGetValue(i, out var blockHash))
    //         {
    //             yield return blockHash;
    //         }
    //         else
    //         {
    //             break;
    //         }
    //     }
    // }

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

    // protected override void Dispose(bool disposing)
    // {
    //     if (disposing)
    //     {
    //         database.Remove(GetKey(chain.Id));
    //     }

    //     base.Dispose(disposing);
    // }

    protected override byte[] GetBytes(BlockHash value) => [.. value.Bytes];

    protected override int GetKey(KeyBytes keyBytes) => BitConverter.ToInt32([.. keyBytes.Bytes]);

    protected override KeyBytes GetKeyBytes(int key) => new(BitConverter.GetBytes(key));

    protected override BlockHash GetValue(byte[] bytes) => new(bytes);
}
