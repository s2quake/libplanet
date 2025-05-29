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

    protected override byte[] ValueToBytes(string value) => Encoding.UTF8.GetBytes(value);

    protected override string BytesToValue(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    protected override string KeyToString(string key) => key;

    protected override string StringToKey(string key) => key;
}
