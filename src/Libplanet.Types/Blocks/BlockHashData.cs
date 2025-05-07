// using System.Security.Cryptography;
// using Libplanet.Serialization;

// namespace Libplanet.Types.Blocks;

// [Model(Version = 1)]
// public sealed record class BlockHashData : IEquatable<BlockHashData>
// {
//     [Property(0)]
//     public required HashDigest<SHA256> StateRootHash { get; init; }

//     [Property(1)]
//     public required ImmutableArray<byte> Signature { get; init; }

//     [Property(2)]
//     public required BlockHash BlockHash { get; init; }

//     [Property(3)]
//     public required HashDigest<SHA256> RawHash { get; init; }

//     [Property(4)]
//     public required HashDigest<SHA256> TxHash { get; init; }

//     [Property(5)]
//     public required HashDigest<SHA256> EvidenceHash { get; init; }

//     public override int GetHashCode() => ModelUtility.GetHashCode(this);

//     public bool Equals(BlockHashData? other) => ModelUtility.Equals(this, other);
// }
