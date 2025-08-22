using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet;

public sealed record class BlockInfo
{
    public static BlockInfo Empty { get; } = new BlockInfo();

    public int Height { get; init; }

    public BlockHash BlockHash { get; init; }

    public HashDigest<SHA256> StateRootHash { get; init; }

    public BlockCommit BlockCommit { get; init; }
}
