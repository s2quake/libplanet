using System.Text;

namespace Libplanet.Data;

public sealed class MetadataIndex(IDatabase database, int cacheSize = 100)
    : IndexBase<string, string>(database.GetOrAdd("metadata"), cacheSize)
{
    protected override byte[] ValueToBytes(string value) => Encoding.UTF8.GetBytes(value);

    protected override string BytesToValue(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    protected override string KeyToString(string key) => key;

    protected override string StringToKey(string key) => key;
}
