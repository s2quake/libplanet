using Libplanet.Serialization;
using Libplanet.Store.Trie;

namespace Libplanet.Store;

public sealed class ChainStore(IDatabase database)
    : CollectionBase<Guid, Chain>(database.GetOrAdd("chains"))
{
    private static readonly object _lock = new();

    public Chain GetOrAdd(Guid chainId)
    {
        lock (_lock)
        {
            if (!TryGetValue(chainId, out var chain))
            {
                chain = new Chain(chainId, database);
                Add(chainId, chain);
            }

            return chain;
        }
    }

    protected override byte[] GetBytes(Chain value)
    {
        var chainDigest = new ChainDigest
        {
            Id = value.Id,
            Height = value.Height,
            BlockCommit = value.BlockCommit,
        };

        return ModelSerializer.SerializeToBytes(chainDigest);
    }

    protected override Guid GetKey(KeyBytes keyBytes) => new(keyBytes.AsSpan());

    protected override KeyBytes GetKeyBytes(Guid key) => new(key.ToByteArray());

    protected override Chain GetValue(byte[] bytes)
    {
        var digest = ModelSerializer.DeserializeFromBytes<ChainDigest>(bytes);
        return new Chain(digest.Id, database)
        {
            BlockCommit = digest.BlockCommit,
            Height = digest.Height,
        };
    }
}
