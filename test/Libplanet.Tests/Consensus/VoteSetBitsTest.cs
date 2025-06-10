// using Libplanet.Net.Consensus;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;

// namespace Libplanet.Tests.Consensus
// {
//     public class VoteSetBitsTest
//     {
//         [Fact]
//         public void InvalidSignature()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));

//             VoteSetBitsMetadata metadata = new VoteSetBitsMetadata(
//                 1,
//                 0,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 new PrivateKey().PublicKey,
//                 VoteFlag.PreVote,
//                 new[] { true, true, false, false });

//             // Empty Signature
//             var emptySigBencodex = metadata.Encoded.Add(
//                 VoteSetBits.SignatureKey,
//                 Array.Empty<byte>());
//             Assert.Throws<ArgumentNullException>(() => new VoteSetBits(emptySigBencodex));

//             // Invalid Signature
//             var invSigBencodex = metadata.Encoded.Add(
//                 VoteSetBits.SignatureKey,
//                 new PrivateKey().Sign(RandomUtility.Bytes(20)));
//             Assert.Throws<ArgumentException>(() => new VoteSetBits(invSigBencodex));
//         }

//         [Fact]
//         public void Sign()
//         {
//             var key = new PrivateKey();
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));

//             VoteSetBitsMetadata metadata = new VoteSetBitsMetadata(
//                 1,
//                 0,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 key.PublicKey,
//                 VoteFlag.PreVote,
//                 new[] { true, true, false, false });
//             VoteSetBits voteSetBits = metadata.Sign(key);

//             Assert.Equal(voteSetBits.Signature, key.Sign(metadata.ByteArray));
//             Assert.True(key.PublicKey.Verify(metadata.ByteArray, voteSetBits.Signature));
//         }
//     }
// }
