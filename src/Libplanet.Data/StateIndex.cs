using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class StateIndex(IDatabase database, int cacheSize = 100)
    : IndexBase<HashDigest<SHA256>, byte[]>(database.GetOrAdd("states"), cacheSize)
{
    public StateIndex()
        : this(new MemoryDatabase())
    {
    }

    protected override byte[] ValueToBytes(byte[] value) => value;

    protected override byte[] BytesToValue(byte[] bytes) => bytes;

    protected override string KeyToString(HashDigest<SHA256> key) => key.ToString();

    protected override HashDigest<SHA256> StringToKey(string key) => HashDigest<SHA256>.Parse(key);
}
