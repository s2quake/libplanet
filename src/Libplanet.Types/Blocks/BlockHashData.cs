using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockHashData : IEquatable<BlockHashData>
{
    public static BlockHashData Empty { get; } = new();

    [Property(0)]
    public HashDigest<SHA256> StateRootHash { get; init; }

    [Property(1)]
    public ImmutableArray<byte> Signature { get; init; }

    [Property(2)]
    public BlockHash BlockHash { get; init; }

    [Property(3)]
    public HashDigest<SHA256> RawHash { get; init; }

    [Property(4)]
    public HashDigest<SHA256> TxHash { get; init; }

    [Property(5)]
    public HashDigest<SHA256> EvidenceHash { get; init; }

    public static BlockHashData Create(PrivateKey privateKey, HashDigest<SHA256> stateRootHash)
    {
        throw new ArgumentNullException(nameof(privateKey));
    }

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public bool Equals(BlockHashData? other) => ModelUtility.Equals(this, other);
}
