using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockHeader
{
    public static BlockHeader Empty { get; } = new();

    public HashDigest<SHA256> StateRootHash { get; init; }

    public ImmutableArray<byte> Signature { get; init; }

    public BlockHash BlockHash { get; init; }

    public int ProtocolVersion { get; init; } = BlockMetadata.CurrentProtocolVersion;

    public long Height { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public Address Proposer { get; init; }

    public BlockHash PreviousHash { get; init; }

    public HashDigest<SHA256> TxHash { get; init; }

    public BlockCommit LastCommit { get; init; } = BlockCommit.Empty;

    public HashDigest<SHA256> EvidenceHash { get; init; }

    public HashDigest<SHA256> RawHash { get; init; }

    public override string ToString() => $"#{Height} {BlockHash}";
}
