using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockContent
    : IValidatableObject
{
    // public BlockContent(
    //     BlockMetadata metadata,
    //     IEnumerable<Transaction> transactions,
    //     IEnumerable<EvidenceBase> evidence)
    // {
    //     // Check if TxHash provided by metadata is valid.
    //     HashDigest<SHA256>? derivedTxHash = DeriveTxHash(transactions);
    //     if (!((Metadata.TxHash is { } a && derivedTxHash is { } b && a.Equals(b)) ||
    //         (Metadata.TxHash is null && derivedTxHash is null)))
    //     {
    //         throw new InvalidBlockTxHashException(
    //             $"The block #{Metadata.Index}'s {nameof(Metadata.TxHash)} is invalid.",
    //             Metadata.TxHash,
    //             derivedTxHash);
    //     }

    //     // Check if transactions are ordered with valid nonces.
    //     transactions.ValidateTxNonces(Metadata.Index);
    //     TxId? prevId = null;
    //     foreach (Transaction tx in transactions)
    //     {
    //         if (prevId is { } prev && prev.CompareTo(tx.Id) > 0)
    //         {
    //             throw new ArgumentException(
    //                 $"Transactions must be ordered by their {nameof(Transaction.Id)}s.",
    //                 nameof(transactions));
    //         }

    //         prevId = tx.Id;
    //     }

    //     ValidateEvidence(metadata, evidence);

    //     Metadata = metadata;
    //     _transactions = transactions;
    //     _evidence = evidence;
    // }

    [Property(0)]
    public required BlockMetadata Metadata { get; init; }

    [Property(1)]
    public ImmutableSortedSet<Transaction> Transactions { get; init; } = [];

    [Property(2)]
    public ImmutableSortedSet<EvidenceBase> Evidence { get; init; } = [];

    public int ProtocolVersion => Metadata.ProtocolVersion;

    public long Index => Metadata.Index;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Miner => Metadata.Miner;

    public PublicKey? PublicKey => Metadata.PublicKey;

    public BlockHash PreviousHash => Metadata.PreviousHash;

    public HashDigest<SHA256>? TxHash => Metadata.TxHash;

    public BlockCommit? LastCommit => Metadata.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => Metadata.EvidenceHash;

    public static HashDigest<SHA256> DeriveTxHash(IEnumerable<Transaction> transactions)
    {
        TxId? prevId = null;
        SHA256 hasher = SHA256.Create();

        // Bencodex lists look like: l...e
        hasher.TransformBlock(new byte[] { 0x6c }, 0, 1, null, 0);  // "l"
        foreach (Transaction tx in transactions)
        {
            if (prevId is { } prev && prev.CompareTo(tx.Id) > 0)
            {
                throw new ArgumentException(
                    $"Transactions must be ordered by their {nameof(Transaction.Id)}s.",
                    nameof(transactions)
                );
            }

            byte[] payload = tx.Serialize();
            hasher.TransformBlock(payload, 0, payload.Length, null, 0);
            prevId = tx.Id;
        }

        if (prevId is null)
        {
            return default;
        }

        hasher.TransformFinalBlock(new byte[] { 0x65 }, 0, 1);  // "e"
        if (hasher.Hash is { } hash)
        {
            return new HashDigest<SHA256>(hash);
        }

        return default;
    }

    public static HashDigest<SHA256>? DeriveEvidenceHash(IEnumerable<EvidenceBase> evidence)
    {
        EvidenceId? prevId = null;
        SHA256 hasher = SHA256.Create();

        // Bencodex lists look like: l...e
        hasher.TransformBlock(new byte[] { 0x6c }, 0, 1, null, 0);  // "l"
        foreach (EvidenceBase ev in evidence)
        {
            if (prevId is { } prev && prev.CompareTo(ev.Id) > 0)
            {
                throw new ArgumentException(
                    $"Transactions must be ordered by their {nameof(Transaction.Id)}s.",
                    nameof(evidence)
                );
            }

            byte[] payload = ModelSerializer.SerializeToBytes(ev);
            hasher.TransformBlock(payload, 0, payload.Length, null, 0);
            prevId = ev.Id;
        }

        if (prevId is null)
        {
            return null;
        }

        hasher.TransformFinalBlock(new byte[] { 0x65 }, 0, 1);  // "e"
        if (hasher.Hash is { } hash)
        {
            return new HashDigest<SHA256>(hash);
        }

        return null;
    }

    public RawBlock Propose()
    {
        var preEvaluationHash = Metadata.DerivePreEvaluationHash();
        var header = new RawBlockHeader(Metadata, preEvaluationHash);
        return new RawBlock { Header = header, Content = this };
    }

    private static void ValidateEvidence(
        BlockMetadata metadata,
        IEnumerable<EvidenceBase> evidence)
    {
        // Check if EvidenceHash provided by metadata is valid.
        HashDigest<SHA256>? evidenceHash = metadata.EvidenceHash;
        HashDigest<SHA256>? derivedEvidenceHash = DeriveEvidenceHash(evidence);
        if (!((evidenceHash is { } e1 && derivedEvidenceHash is { } e2 && e1.Equals(e2)) ||
            (evidenceHash is null && derivedEvidenceHash is null)))
        {
            throw new InvalidOperationException(
                $"The block #{metadata.Index}'s {nameof(metadata.EvidenceHash)} is invalid.");
        }

        // Check if transactions are ordered with valid nonces.
        EvidenceId? evidenceId = null;
        foreach (EvidenceBase ev in evidence)
        {
            if (evidenceId is { } prev && prev.CompareTo(ev.Id) > 0)
            {
                throw new ArgumentException(
                    $"Evidence must be ordered by their {nameof(EvidenceBase.Id)}s.",
                    nameof(ev));
            }

            evidenceId = ev.Id;
        }
    }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        yield break;
        // Check if TxHash provided by metadata is valid.
        // HashDigest<SHA256>? derivedTxHash = DeriveTxHash(Transactions);
        // if (!((Metadata.TxHash is { } a && derivedTxHash is { } b && a.Equals(b)) ||
        //     (Metadata.TxHash is null && derivedTxHash is null)))
        // {
        //     throw new InvalidOperationException(
        //         $"The block #{Metadata.Index}'s {nameof(Metadata.TxHash)} is invalid.");
        // }

        // // Check if transactions are ordered with valid nonces.
        // Transactions.ValidateTxNonces(Metadata.Index);
        // TxId? prevId = null;
        // foreach (Transaction tx in Transactions)
        // {
        //     if (prevId is { } prev && prev.CompareTo(tx.Id) > 0)
        //     {
        //         throw new ArgumentException(
        //             $"Transactions must be ordered by their {nameof(Transaction.Id)}s.",
        //             nameof(Transactions));
        //     }

        //     prevId = tx.Id;
        // }

        // ValidateEvidence(Metadata, Evidence);
    }
}
