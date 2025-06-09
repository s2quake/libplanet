using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.ModelConverters;
using Libplanet.Types;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Data.Structures.Nodes;

[ModelConverter(typeof(HashNodeModelConverter), "hnode")]
internal sealed record class HashNode : INode
{
    private const int _cacheSize = 524_288;

    private static readonly ICache<HashDigest<SHA256>, byte[]> _cache
        = new ConcurrentLruBuilder<HashDigest<SHA256>, byte[]>()
            .WithMetrics()
            .WithExpireAfterAccess(TimeSpan.FromMinutes(10))
            .WithCapacity(_cacheSize)
            .Build();

    public required HashDigest<SHA256> Hash { get; init; }

    public required ITable Table { get; init; }

    IEnumerable<INode> INode.Children
    {
        get
        {
            yield return Expand();
        }
    }

    public override int GetHashCode() => Hash.GetHashCode();

    public INode Expand()
    {
        if (!TryGetValue(Hash, out var bytes))
        {
            var key = Hash.ToString();
            if (Table.TryGetValue(key, out var valueBytes))
            {
                bytes = valueBytes;
                AddOrUpdate(Hash, valueBytes);
            }
            else
            {
                return NullNode.Value;
            }
        }

        var context = new ModelOptions
        {
            Items = ImmutableDictionary<object, object?>.Empty.Add(typeof(ITable), Table),
        };

        var node = ModelSerializer.DeserializeFromBytes<INode>(bytes, context);
        return node;
    }

    public static bool TryGetValue(HashDigest<SHA256> hash, [MaybeNullWhen(false)] out byte[] value)
        => _cache.TryGet(hash, out value);

    public static void AddOrUpdate(HashDigest<SHA256> hash, byte[] value)
        => _cache.AddOrUpdate(hash, value);
}
