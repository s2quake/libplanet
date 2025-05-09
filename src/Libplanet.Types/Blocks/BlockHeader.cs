using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockHeader
{
    public const int CurrentProtocolVersion = 0;

    [Property(0)]
    public int ProtocolVersion { get; init; } = CurrentProtocolVersion;

    [Property(1)]
    public int Height { get; init; }

    [Property(2)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(3)]
    public Address Proposer { get; init; }

    [Property(4)]
    public BlockHash PreviousHash { get; init; }

    [Property(5)]
    public BlockCommit LastCommit { get; init; } = BlockCommit.Empty;

    public HashDigest<SHA256> Hash => HashDigest<SHA256>.Create(ModelSerializer.SerializeToBytes(this));
}
