using System.Text;

namespace Libplanet.Data;

public sealed class ImmutableMetadataIndex(IDatabase database, int cacheSize = 100)
    : IndexBase<string, string>(database.GetOrAdd("immutable_metadata"), cacheSize)
{
    internal const string Name = "immutable_metadata";

    protected override byte[] ValueToBytes(string value) => Encoding.UTF8.GetBytes(value);

    protected override string BytesToValue(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    protected override string KeyToString(string key) => key;

    protected override string StringToKey(string key) => key;
}
