using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockHeader(
    [property: Property(0)] PreEvaluationBlockHeader PreEvaluationBlockHeader,
    [property: Property(1)] HashDigest<SHA256> StateRootHash,
    [property: Property(2)] ImmutableArray<byte> Signature,
    [property: Property(3)] BlockHash BlockHash)
{
    // public BlockHeader(
    //     PreEvaluationBlockHeader preEvaluationBlockHeader,
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

    //     _preEvaluationBlockHeader = preEvaluationBlockHeader;
    //     _stateRootHash = proof.StateRootHash;
    //     _signature = proof.Signature;
    //     _hash = proof.Hash;
    // }

    public int ProtocolVersion => PreEvaluationBlockHeader.ProtocolVersion;

    public long Index => PreEvaluationBlockHeader.Index;

    public DateTimeOffset Timestamp => PreEvaluationBlockHeader.Timestamp;

    public Address Miner => PreEvaluationBlockHeader.Miner;

    public PublicKey? PublicKey => PreEvaluationBlockHeader.PublicKey;

    public BlockHash PreviousHash => PreEvaluationBlockHeader.PreviousHash;

    public HashDigest<SHA256>? TxHash => PreEvaluationBlockHeader.TxHash;

    public BlockCommit? LastCommit => PreEvaluationBlockHeader.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => PreEvaluationBlockHeader.EvidenceHash;

    public HashDigest<SHA256> PreEvaluationHash => PreEvaluationBlockHeader.PreEvaluationHash;

    public override string ToString() => $"#{Index} {BlockHash}";
}
