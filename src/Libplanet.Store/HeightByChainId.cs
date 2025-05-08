using Libplanet.Store.Trie;

namespace Libplanet.Store;

public sealed class HeightByChainId(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<Guid, long>(dictionary)
{
    protected override byte[] GetBytes(long value) => BitConverter.GetBytes(value);

    protected override Guid GetKey(KeyBytes keyBytes) => new(keyBytes.AsSpan());

    protected override KeyBytes GetKeyBytes(Guid key) => new(key.ToByteArray());

    protected override long GetValue(byte[] bytes) => BitConverter.ToInt64(bytes.AsSpan());
}
