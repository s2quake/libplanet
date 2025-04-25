// using System.Security.Cryptography;
// using Bencodex.Types;
// using Libplanet.Common;
// using Libplanet.Crypto;

// namespace Libplanet.Types.Blocks;

// public sealed record class RawBlockHeader
// {
//     private static readonly Codec Codec = new();

//     // public required BlockMetadata Metadata { get; init; }

//     public required HashDigest<SHA256> RawHash { get; init; }

//     // public int ProtocolVersion => Metadata.ProtocolVersion;

//     // public long Index => Metadata.Index;

//     // public DateTimeOffset Timestamp => Metadata.Timestamp;

//     // public Address Miner => Metadata.Miner;

//     // public PublicKey? PublicKey => Metadata.PublicKey;

//     // public BlockHash PreviousHash => Metadata.PreviousHash;

//     // public HashDigest<SHA256>? TxHash => Metadata.TxHash;

//     // public BlockCommit? LastCommit => Metadata.LastCommit;

//     // public HashDigest<SHA256>? EvidenceHash => Metadata.EvidenceHash;

//     // public Bencodex.Types.Dictionary MakeCandidateData(
//     //     HashDigest<SHA256> stateRootHash,
//     //     ImmutableArray<byte>? signature = null)
//     // {
//     //     throw new NotImplementedException();
//     //     // Dictionary dict = Metadata.MakeCandidateData()
//     //     //     .Add("state_root_hash", stateRootHash.ByteArray);
//     //     // if (signature is { } sig)
//     //     // {
//     //     //     dict = dict.Add("signature", sig);
//     //     // }

//     //     // return dict;
//     // }

    

//     // public BlockHash DeriveBlockHash(
//     //     in HashDigest<SHA256> stateRootHash,
//     //     in ImmutableArray<byte>? signature
//     // ) =>
//     //     BlockHash.DeriveFrom(Codec.Encode(MakeCandidateData(stateRootHash, signature)));

//     // public void ValidateTimestamp() => ValidateTimestamp(DateTimeOffset.UtcNow);

//     // public void ValidateTimestamp(DateTimeOffset currentTime)
//     //     => Metadata.ValidateTimestamp(currentTime);

//     private static HashDigest<SHA256> CheckPreEvaluationHash(
//         BlockMetadata metadata,
//         in HashDigest<SHA256> preEvaluationHash)
//     {
//         if (metadata.ProtocolVersion < BlockMetadata.PBFTProtocolVersion)
//         {
//             return preEvaluationHash;
//         }
//         else
//         {
//             HashDigest<SHA256> expected = metadata.DerivePreEvaluationHash();
//             return expected.Equals(preEvaluationHash)
//                 ? expected
//                 : throw new InvalidOperationException(
//                     $"Given {nameof(preEvaluationHash)} {preEvaluationHash} does not match " +
//                     $"the expected value {expected}.");
//         }
//     }
// }
