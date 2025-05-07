using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockContent : IEquatable<BlockContent>, IValidatableObject
{
    [Property(0)]
    public ImmutableSortedSet<Transaction> Transactions { get; init; } = [];

    [Property(1)]
    public ImmutableSortedSet<EvidenceBase> Evidences { get; init; } = [];

    public HashDigest<SHA256> TxHash => DeriveTxHash(Transactions);

    public HashDigest<SHA256> EvidenceHash => DeriveEvidenceHash(Evidences);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public bool Equals(BlockContent? other) => ModelUtility.Equals(this, other);

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        yield break;
    }

    private static HashDigest<SHA256> DeriveTxHash(ImmutableSortedSet<Transaction> transactions)
    {
        if (transactions.Count == 0)
        {
            return default;
        }

        using var hasher = SHA256.Create();
        hasher.TransformBlock("t"u8.ToArray(), 0, 1, null, 0);
        foreach (var transaction in transactions)
        {
            var payload = ModelSerializer.SerializeToBytes(transaction);
            hasher.TransformBlock(payload, 0, payload.Length, null, 0);
        }

        hasher.TransformFinalBlock("x"u8.ToArray(), 0, 1);
        if (hasher.Hash is { } hash)
        {
            return new HashDigest<SHA256>(hash);
        }

        throw new UnreachableException(
            "The hash of transactions should not be null. " +
            "Please check the implementation of DeriveTxHash.");
    }

    private static HashDigest<SHA256> DeriveEvidenceHash(ImmutableSortedSet<EvidenceBase> evidence)
    {
        if (evidence.Count == 0)
        {
            return default;
        }

        using var hasher = SHA256.Create();
        hasher.TransformBlock("e"u8.ToArray(), 0, 1, null, 0);
        foreach (var ev in evidence)
        {
            var payload = ModelSerializer.SerializeToBytes(ev);
            hasher.TransformBlock(payload, 0, payload.Length, null, 0);
        }

        hasher.TransformFinalBlock("v"u8.ToArray(), 0, 1);
        if (hasher.Hash is { } hash)
        {
            return new HashDigest<SHA256>(hash);
        }

        throw new UnreachableException(
            "The hash of evidence should not be null. " +
            "Please check the implementation of DeriveEvidenceHash.");
    }
}
