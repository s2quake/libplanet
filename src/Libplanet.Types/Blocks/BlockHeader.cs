using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockHeader
{
    public static BlockHeader Empty { get; } = new();

    [Property(0)]
    public HashDigest<SHA256> StateRootHash { get; init; }

    [Property(1)]
    public ImmutableArray<byte> Signature { get; init; }

    [Property(2)]
    public BlockHash BlockHash { get; init; }

    [Property(3)]
    public int ProtocolVersion { get; init; } = BlockMetadata.CurrentProtocolVersion;

    [Property(4)]
    public long Height { get; init; }

    [Property(5)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(6)]
    public Address Proposer { get; init; }

    [Property(7)]
    public BlockHash PreviousHash { get; init; }

    [Property(8)]
    public BlockCommit LastCommit { get; init; } = BlockCommit.Empty;

    [Property(9)]
    public HashDigest<SHA256> RawHash { get; init; }

    [Property(10)]
    public HashDigest<SHA256> TxHash { get; init; }

    [Property(11)]
    public HashDigest<SHA256> EvidenceHash { get; init; }

    public override string ToString() => $"#{Height} {BlockHash}";
}
