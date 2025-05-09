using Libplanet.Store.Trie;

namespace Libplanet.Store;

public sealed class HeightByChainId(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<Guid, int>(dictionary)
{
    protected override byte[] GetBytes(int value) => BitConverter.GetBytes(value);

    protected override Guid GetKey(KeyBytes keyBytes) => new(keyBytes.AsSpan());

    protected override KeyBytes GetKeyBytes(Guid key) => new(key.ToByteArray());

    protected override int GetValue(byte[] bytes) => BitConverter.ToInt32(bytes.AsSpan());
}
