using System.Globalization;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Assets;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

public sealed record class BlockMetadata
{
    public const int CurrentProtocolVersion = 9;

    public const int TransferFixProtocolVersion = 1;

    public const int SignatureProtocolVersion = 2;

    public const int TransactionOrderingFixProtocolVersion = 3;

    public const int PBFTProtocolVersion = 4;

    public const int WorldStateProtocolVersion = 5;

    public const int ValidatorSetAccountProtocolVersion = 6;

    public const int CurrencyAccountProtocolVersion = 7;

    public const int SlothProtocolVersion = 8;

    public const int EvidenceProtocolVersion = 9;

    public int ProtocolVersion { get; init; }

    public long Index { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public Address Miner { get; init; }

    public PublicKey? PublicKey { get; init; }

    public BlockHash PreviousHash { get; init; }

    public HashDigest<SHA256> TxHash { get; init; }

    public BlockCommit? LastCommit { get; init; }

    public HashDigest<SHA256>? EvidenceHash { get; init; }

    // public Bencodex.Types.Dictionary MakeCandidateData()
    // {
    //     var dict = Bencodex.Types.Dictionary.Empty
    //         .Add("index", Index)
    //         .Add("timestamp", Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture))
    //         .Add("nonce", ImmutableArray<byte>.Empty);  // For backward-compatibility.

    //     if (ProtocolVersion != 0)
    //     {
    //         dict = dict.Add("protocol_version", ProtocolVersion);
    //     }

    //     if (PreviousHash is { } prevHash)
    //     {
    //         dict = dict.Add("previous_hash", prevHash.ByteArray);
    //     }

    //     if (TxHash is { } txHash)
    //     {
    //         dict = dict.Add("transaction_fingerprint", txHash.ByteArray);
    //     }

    //     if (LastCommit is { } lastCommit)
    //     {
    //         dict = dict.Add("last_commit", lastCommit.ToHash().ByteArray);
    //     }

    //     if (EvidenceHash is { } evidenceHash)
    //     {
    //         dict = dict.Add("evidence_hash", evidenceHash.ByteArray);
    //     }

    //     // As blocks hadn't been signed before ProtocolVersion <= 1, the PublicKey property
    //     // is nullable type-wise.  Blocks with ProtocolVersion <= 1 had a `reward_beneficiary`
    //     // field, which referred to the Miner address.  On the other hand, blocks with
    //     // ProtocolVersion >= 2 have a `public_key` field instead.  (As Miner addresses can be
    //     // derived from PublicKeys, we don't need to include both at a time.)  The PublicKey
    //     // property in this class guarantees that its ProtocolVersion is <= 1 when it is null
    //     // and its ProtocolVersion is >= 2 when it is not null:
    //     dict = PublicKey is { } pubKey && ProtocolVersion >= 2
    //         ? dict.Add("public_key", pubKey.ToByteArray(compress: true)) // ProtocolVersion >= 2
    //         : dict.Add("reward_beneficiary", Miner.ToBencodex()); /////// ProtocolVersion <= 1

    //     return dict;
    // }

    public HashDigest<SHA256> DerivePreEvaluationHash()
        => HashDigest<SHA256>.DeriveFrom(ModelSerializer.SerializeToBytes(this));

    // private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
    // private static readonly Codec Codec = new Codec();

    // public BlockMetadata(
    //     long index,
    //     DateTimeOffset timestamp,
    //     PublicKey publicKey,
    //     BlockHash previousHash,
    //     HashDigest<SHA256>? txHash,
    //     BlockCommit? lastCommit,
    //     HashDigest<SHA256>? evidenceHash)
    //     : this(
    //         protocolVersion: CurrentProtocolVersion,
    //         index: index,
    //         timestamp: timestamp,
    //         miner: publicKey.Address,
    //         publicKey: publicKey,
    //         previousHash: previousHash,
    //         txHash: txHash,
    //         lastCommit: lastCommit,
    //         evidenceHash: evidenceHash)
    // {
    // }

    // public BlockMetadata(
    //     int protocolVersion,
    //     long index,
    //     DateTimeOffset timestamp,
    //     Address miner,
    //     PublicKey? publicKey,
    //     BlockHash previousHash,
    //     HashDigest<SHA256>? txHash,
    //     BlockCommit? lastCommit,
    //     HashDigest<SHA256>? evidenceHash)
    // {
    //     // Protocol version validity check.
    //     if (protocolVersion < 0)
    //     {
    //         throw new InvalidBlockProtocolVersionException(
    //             $"A block's protocol version cannot be less than zero: {protocolVersion}.",
    //             protocolVersion);
    //     }
    //     else if (protocolVersion > CurrentProtocolVersion)
    //     {
    //         throw new InvalidBlockProtocolVersionException(
    //             "A block's protocol version cannot be greater than " +
    //             $"{CurrentProtocolVersion}: {protocolVersion}.",
    //             protocolVersion);
    //     }
    //     else
    //     {
    //         ProtocolVersion = protocolVersion;
    //     }

    //     // Index validity check.
    //     Index = index < 0L
    //         ? throw new InvalidBlockIndexException(
    //             $"A negative index is not allowed: {index}.")
    //         : index;

    //     // FIXME: Transaction timestamps do not convert to universal time.
    //     Timestamp = timestamp.ToUniversalTime();

    //     // Public key and miner validity checks.
    //     if (protocolVersion >= 2)
    //     {
    //         PublicKey = publicKey is { } p
    //             ? p
    //             : throw new InvalidBlockPublicKeyException(
    //                 $"Argument {nameof(publicKey)} cannot be null for " +
    //                 $"{nameof(protocolVersion)} >= 2.",
    //                 publicKey);
    //         Miner = miner == p.Address
    //             ? miner
    //             : throw new InvalidBlockPublicKeyException(
    //                 $"Argument {nameof(miner)} should match the derived address of " +
    //                 $"{nameof(publicKey)} for {nameof(protocolVersion)} >= 2.",
    //                 publicKey);
    //     }
    //     else
    //     {
    //         PublicKey = publicKey is null
    //             ? (PublicKey?)null
    //             : throw new InvalidBlockPublicKeyException(
    //                 $"Argument {nameof(publicKey)} should be null for " +
    //                 $"{nameof(protocolVersion)} < 2.",
    //                 publicKey);
    //         Miner = miner;
    //     }

    //     // Previous hash validity checks.
    //     if (index == 0 ^ (previousHash == default))
    //     {
    //         throw new InvalidOperationException(
    //             $"{nameof(previousHash)} can be null if and only if {nameof(index)} is 0.");
    //     }
    //     else
    //     {
    //         PreviousHash = previousHash;
    //     }

    //     // LastCommit checks.
    //     if (lastCommit is { } commit)
    //     {
    //         if (commit.Height != index - 1)
    //         {
    //             throw new InvalidOperationException(
    //                 $"The lastcommit height {commit.Height} of block #{index} " +
    //                 $"should match the previous block's index {index - 1}.");
    //         }
    //         else if (!commit.BlockHash.Equals(previousHash))
    //         {
    //             throw new InvalidOperationException(
    //                 $"The lastcommit blockhash {commit.BlockHash} of block #{index} " +
    //                 $"should match the previous block's hash {previousHash}.");
    //         }
    //     }

    //     TxHash = txHash;
    //     LastCommit = lastCommit;
    //     EvidenceHash = evidenceHash;
    // }
}
