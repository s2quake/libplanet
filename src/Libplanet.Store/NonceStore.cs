using Libplanet.Store.Trie;
using Libplanet.Types.Crypto;

namespace Libplanet.Store;

public sealed class NonceStore(Guid chainId, IDatabase database)
    : CollectionBase<Address, long>(database.GetOrAdd(GetKey(chainId)))
{
    public long Increase(Address key, long delta = 1L) => this[key] = this.GetValueOrDefault(key) + delta;

    public void MergeFrom(NonceStore source)
    {
        foreach (var (key, value) in source)
        {
            if (TryGetValue(key, out var nonce))
            {
                this[key] = Math.Max(nonce, value);
            }
            else
            {
                this[key] = value;
            }
        }
    }

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
