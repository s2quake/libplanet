using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockHeader(
    [property: Property(0)] RawBlockHeader RawBlockHeader,
    [property: Property(1)] HashDigest<SHA256> StateRootHash,
    [property: Property(2)] ImmutableArray<byte> Signature,
    [property: Property(3)] BlockHash BlockHash)
{
    // public BlockHeader(
    //     RawBlockHeader rawBlockHeader,
    //     (
    //         HashDigest<SHA256> StateRootHash,
    //         ImmutableArray<byte>? Signature,
    //         BlockHash Hash
    //     ) proof
    // )
    // {
    //     BlockHash expectedHash =
    //         preEvaluationBlockPreEvaluationBlockHeader.DeriveBlockHash(proof.StateRootHash, proof.Signature);
    //     if (preEvaluationBlockPreEvaluationBlockHeader.ProtocolVersion < BlockMetadata.PBFTProtocolVersion)
    //     {
    //         // Skip verifying signature for PoW blocks due to change of the block structure.
    //         // If verification is required, use older version of LibPlanet(<0.43).
    //     }
    //     else if (
    //         !preEvaluationBlockPreEvaluationBlockHeader.VerifySignature(proof.Signature, proof.StateRootHash))
    //     {
    //         long idx = preEvaluationBlockPreEvaluationBlockHeader.Index;
    //         string msg = preEvaluationBlockPreEvaluationBlockHeader.ProtocolVersion >=
    //             BlockMetadata.SignatureProtocolVersion
    //                 ? $"The block #{idx} #{proof.Hash}'s signature is invalid."
    //                 : $"The block #{idx} #{proof.Hash} cannot be signed as its " +
    //                   $"protocol version is less than " +
    //                   $"{BlockMetadata.SignatureProtocolVersion}: " +
    //                   $"{preEvaluationBlockPreEvaluationBlockHeader.ProtocolVersion}.";
    //         throw new InvalidBlockSignatureException(
    //             msg,
    //             preEvaluationBlockPreEvaluationBlockHeader.PublicKey,
    //             proof.Signature);
    //     }
    //     else if (!proof.Hash.Equals(expectedHash))
    //     {
    //         throw new InvalidBlockHashException(
    //             $"The block #{preEvaluationBlockPreEvaluationBlockHeader.Index} {proof.Hash} has " +
    //             $"an invalid hash; expected: {expectedHash}.");
    //     }

    //     _preEvaluationBlockHeader = rawBlockHeader;
    //     _stateRootHash = proof.StateRootHash;
    //     _signature = proof.Signature;
    //     _hash = proof.Hash;
    // }

    public int ProtocolVersion => RawBlockHeader.ProtocolVersion;

    public long Index => RawBlockHeader.Index;

    public DateTimeOffset Timestamp => RawBlockHeader.Timestamp;

    public Address Miner => RawBlockHeader.Miner;

    public PublicKey? PublicKey => RawBlockHeader.PublicKey;

    public BlockHash PreviousHash => RawBlockHeader.PreviousHash;

    public HashDigest<SHA256>? TxHash => RawBlockHeader.TxHash;

    public BlockCommit? LastCommit => RawBlockHeader.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => RawBlockHeader.EvidenceHash;

    public HashDigest<SHA256> RawHash => RawBlockHeader.RawHash;

    public override string ToString() => $"#{Index} {BlockHash}";
}
