using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Data;

public sealed class StateRootHashIndex(IDatabase database)
    : IndexBase<BlockHash, HashDigest<SHA256>>(database.GetOrAdd("state_root_hash"))
{
    protected override byte[] GetBytes(HashDigest<SHA256> value) => [.. value.Bytes];

    protected override HashDigest<SHA256> GetValue(byte[] bytes) => new(bytes);
}
