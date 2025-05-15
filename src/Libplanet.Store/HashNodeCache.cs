using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Types;

namespace Libplanet.Store;

internal static class HashNodeCache
{
    private const int _cacheSize = 524_288;

    private static readonly ICache<HashDigest<SHA256>, byte[]> _cache
        = new ConcurrentLruBuilder<HashDigest<SHA256>, byte[]>()
            .WithMetrics()
            .WithExpireAfterAccess(TimeSpan.FromMinutes(10))
            .WithCapacity(_cacheSize)
            .Build();

    public static bool TryGetValue(HashDigest<SHA256> hash, [MaybeNullWhen(false)] out byte[] value)
        => _cache.TryGet(hash, out value);

    public static void AddOrUpdate(HashDigest<SHA256> hash, byte[] value)
        => _cache.AddOrUpdate(hash, value);
}
