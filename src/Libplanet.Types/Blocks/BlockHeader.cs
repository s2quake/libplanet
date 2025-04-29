using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockHeader
{
    public static BlockHeader Empty { get; } = new();
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
    //         throw new InvalidOperationException(
    //             $"The block #{preEvaluationBlockPreEvaluationBlockHeader.Index} {proof.Hash} has " +
    //             $"an invalid hash; expected: {expectedHash}.");
    //     }

    //     _preEvaluationBlockHeader = rawBlockHeader;
    //     _stateRootHash = proof.StateRootHash;
    //     _signature = proof.Signature;
    //     _hash = proof.Hash;
    // }

    public HashDigest<SHA256> StateRootHash { get; init; }

    public ImmutableArray<byte> Signature { get; init; }

    public BlockHash BlockHash { get; init; }

    public int ProtocolVersion { get; init; }

    public long Height { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public Address Miner { get; init; }

    public PublicKey? PublicKey { get; init; }

    public BlockHash PreviousHash { get; init; }

    public HashDigest<SHA256> TxHash { get; init; }

    public BlockCommit LastCommit { get; init; } = BlockCommit.Empty;

    public HashDigest<SHA256> EvidenceHash { get; init; }

    public HashDigest<SHA256> RawHash { get; init; }

    public override string ToString() => $"#{Height} {BlockHash}";

    // public void ValidateTimestamp() => RawBlockHeader.Metadata.ValidateTimestamp();

    // public void ValidateTimestamp(DateTimeOffset currentTime)
    //     => RawBlockHeader.Metadata.ValidateTimestamp(currentTime);
}
