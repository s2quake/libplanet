using System.Text;

namespace Libplanet.Data;

public sealed class MetadataIndex : IndexBase<string, string>
{
    public MetadataIndex(IDatabase database)
        : base(database.GetOrAdd("metadata"))
    {
    }

    public MetadataIndex(Guid chainId, IDatabase database)
        : base(database.GetOrAdd($"{chainId}_metadata"))
    {
    }

    protected override byte[] GetBytes(string value) => Encoding.UTF8.GetBytes(value);

    protected override string GetValue(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
