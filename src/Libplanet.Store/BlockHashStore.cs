using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockHashStore(Chain chain, IDatabase database)
    : StoreBase<int, BlockHash>(database.GetOrAdd(GetKey(chain.Id)))
{
    private readonly MetadataStore _metadata = chain.Metadata;

    public int GenesisHeight
    {
        get => int.Parse(_metadata.GetValueOrDefault("genesisHeight", "0"));
        set => _metadata["genesisHeight"] = value.ToString();
    }

    public int Height
    {
        get => int.Parse(_metadata.GetValueOrDefault("height", "0"));
        set => _metadata["height"] = value.ToString();
    }

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
            database.Remove(GetKey(chain.Id));
        }

        base.Dispose(disposing);
    }

    protected override byte[] GetBytes(BlockHash value) => [.. value.Bytes];

    protected override int GetKey(KeyBytes keyBytes) => BitConverter.ToInt32([.. keyBytes.Bytes]);

    protected override KeyBytes GetKeyBytes(int key) => new(BitConverter.GetBytes(key));

    protected override BlockHash GetValue(byte[] bytes) => new(bytes);
}
