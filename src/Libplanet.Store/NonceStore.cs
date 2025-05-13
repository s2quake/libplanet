using Libplanet.Store.Trie;
using Libplanet.Types.Crypto;

namespace Libplanet.Store;

public sealed class NonceStore(Guid chainId, IDatabase database)
    : CollectionBase<Address, long>(database.GetOrAdd($"{chainId}_nonces"))
{
    public long GetOrDefault(Address signer)
    {
        if (TryGetValue(signer, out long value))
        {
            return value;
        }

        return 0L;
    }

    public long Increase(Address signer, long delta = 1L)
    {
        long nonce = GetOrDefault(signer);
        nonce += delta;
        this[signer] = nonce;
        return nonce;
    }

    protected override byte[] GetBytes(long value) => BitConverter.GetBytes(value);

    protected override Address GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(Address key) => new(key.Bytes);

    protected override long GetValue(byte[] bytes) => BitConverter.ToInt64(bytes, 0);
}
