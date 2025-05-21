using System.Text;
using Libplanet.Store.Trie;

namespace Libplanet.Store;

public sealed class MetadataStore : StoreBase<string, string>
{
    public MetadataStore(IDatabase database)
        : base(database.GetOrAdd("metadata"))
    {
    }

    public MetadataStore(Guid chainId, IDatabase database)
        : base(database.GetOrAdd($"{chainId}_metadata"))
    {
    }

    protected override byte[] GetBytes(string value) => Encoding.UTF8.GetBytes(value);

    protected override string GetKey(KeyBytes keyBytes) => Encoding.UTF8.GetString(keyBytes.AsSpan());

    protected override KeyBytes GetKeyBytes(string key) => new(Encoding.UTF8.GetBytes(key));

    protected override string GetValue(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
