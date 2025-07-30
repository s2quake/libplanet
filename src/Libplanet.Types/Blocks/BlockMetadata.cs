using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockMetadata
{
    public const int CurrentProtocolVersion = 9;

    private static readonly TimeSpan TimestampThreshold = TimeSpan.FromSeconds(15);

    [Property(0)]
    public int ProtocolVersion { get; init; }

    [Property(1)]
    public long Height { get; init; }

    [Property(2)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(3)]
    [NonDefault]
    public Address Miner { get; init; }

    [Property(4)]
    public BlockHash PreviousHash { get; init; }

    [Property(5)]
    public HashDigest<SHA256> TxHash { get; init; }

    [Property(6)]
    public BlockCommit LastCommit { get; init; } = BlockCommit.Empty;

    [Property(7)]
    public HashDigest<SHA256> EvidenceHash { get; init; }

    public static explicit operator BlockMetadata(BlockHeader header)
    {
        return new BlockMetadata
        {
            ProtocolVersion = header.ProtocolVersion,
            Height = header.Height,
            Timestamp = header.Timestamp,
            Miner = header.Miner,
            // PublicKey = header.PublicKey,
            PreviousHash = header.PreviousHash,
            TxHash = header.TxHash,
            LastCommit = header.LastCommit,
            EvidenceHash = header.EvidenceHash,
        };
    }

    public void ValidateTimestamp() => ValidateTimestamp(DateTimeOffset.UtcNow);

    public void ValidateTimestamp(DateTimeOffset currentTime)
    {
        if (currentTime + TimestampThreshold < Timestamp)
        {
            var message = $"The block #{Height}'s timestamp " +
                $"({Timestamp}) is later than now " +
                $"({currentTime}, threshold: {TimestampThreshold}).";
            throw new InvalidOperationException(message);
            // string hash = metadata is BlockExcerpt h
            //     ? $" {h.Hash}"
            //     : string.Empty;
            // throw new InvalidOperationException(
            //     $"The block #{metadata.Index}{hash}'s timestamp " +
            //     $"({metadata.Timestamp}) is later than now ({currentTime}, " +
            //     $"threshold: {TimestampThreshold}).");
        }
    }

    public BlockHash DeriveBlockHash(
        in HashDigest<SHA256> stateRootHash,
        in ImmutableArray<byte> signature)
    {
        var list = new List(
            ModelSerializer.Serialize(this),
            ModelSerializer.Serialize(stateRootHash),
            ModelSerializer.Serialize(signature));
        return BlockHash.DeriveFrom(BencodexUtility.Encode(list));
    }

    public ImmutableArray<byte> MakeSignature(
        PrivateKey privateKey,
        HashDigest<SHA256> stateRootHash)
    {
        // if (PublicKey is null)
        // {
        //     throw new InvalidOperationException(
        //         "The block with the protocol version < 2 cannot be signed, because it lacks " +
        //         "its miner's public key so that others cannot verify its signature."
        //     );
        // }
        // else if (!privateKey.PublicKey.Equals(PublicKey))
        // {
        //     string m = "The given private key does not match to the proposer's public key." +
        //         $"Block's public key: {PublicKey}\n" +
        //         $"Derived public key: {privateKey.PublicKey}\n";
        //     throw new ArgumentException(m, nameof(privateKey));
        // }

        byte[] msg = ModelSerializer.SerializeToBytes(stateRootHash);
        byte[] sig = privateKey.Sign(msg);
        return ImmutableArray.Create(sig);
    }

    public bool VerifySignature(
        ImmutableArray<byte> signature,
        HashDigest<SHA256> stateRootHash)
    {
        var message = ModelSerializer.SerializeToBytes(stateRootHash).ToImmutableArray();
        return PublicKey.Verify(Miner, message, signature);
        //     return pubKey.Verify(msg, sig);

        // if (PublicKey is { } pubKey && signature is { } sig)
        // {
        //     var msg = ModelSerializer.SerializeToBytes(stateRootHash).ToImmutableArray();
        //     return pubKey.Verify(msg, sig);
        // }
        // else if (PublicKey is null)
        // {
        //     return signature is null;
        // }

        // return false;
    }

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
