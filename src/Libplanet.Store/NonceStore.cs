using Libplanet.Store.Trie;
using Libplanet.Types.Crypto;

namespace Libplanet.Store;

public sealed class NonceStore(Guid chainId, IDatabase database)
    : CollectionBase<Address, long>(database.GetOrAdd(GetKey(chainId)))
{
    public long GetOrDefault(Address signer)
    {
        if (TryGetValue(signer, out long value))
        {
            return value;
        }

        return 0L;
    }

    public long Increase(Address signer, long delta = 1L) => this[signer] = GetOrDefault(signer) + delta;

    internal static string GetKey(Guid chainId) => $"{chainId}_nonces";

    protected override byte[] GetBytes(long value) => BitConverter.GetBytes(value);

    protected override Address GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(Address key) => new(key.Bytes);

    protected override long GetValue(byte[] bytes) => BitConverter.ToInt64(bytes, 0);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            database.Remove(GetKey(chainId));
        }

        base.Dispose(disposing);
    }
}
