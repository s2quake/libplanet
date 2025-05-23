using System.Text;

namespace Libplanet.Data;

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

    protected override string GetValue(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
