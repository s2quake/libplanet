using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class StateRootHashIndex(IDatabase database)
    : IndexBase<BlockHash, HashDigest<SHA256>>(database.GetOrAdd("state_root_hash"))
{
    protected override byte[] ValueToBytes(HashDigest<SHA256> value) => [.. value.Bytes];

    protected override HashDigest<SHA256> BytesToValue(byte[] bytes) => new(bytes);

    protected override string KeyToString(BlockHash key) => key.ToString();

    protected override BlockHash StringToKey(string key) => BlockHash.Parse(key);
}
