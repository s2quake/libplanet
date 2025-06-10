// using Libplanet.Net.Consensus;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;

// namespace Libplanet.Tests.Consensus
// {
//     public class Maj23Test
//     {
//         [Fact]
//         public void InvalidSignature()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));

//             Maj23Metadata metadata = new Maj23Metadata(
//                 1,
//                 0,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 new PrivateKey().PublicKey,
//                 VoteFlag.PreVote);

//             // Empty Signature
//             var emptySigBencodex = metadata.Encoded.Add(Maj23.SignatureKey, Array.Empty<byte>());
//             Assert.Throws<ArgumentNullException>(() => new Maj23(emptySigBencodex));

//             // Invalid Signature
//             var invSigBencodex = metadata.Encoded.Add(
//                 Maj23.SignatureKey,
//                 new PrivateKey().Sign(RandomUtility.Bytes(20)));
//             Assert.Throws<ArgumentException>(() => new Maj23(invSigBencodex));
//         }

//         [Fact]
//         public void Sign()
//         {
//             var key = new PrivateKey();
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));

//             Maj23Metadata metadata = new Maj23Metadata(
//                 1,
//                 0,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 key.PublicKey,
//                 VoteFlag.PreVote);
//             Maj23 maj23 = metadata.Sign(key);

//             Assert.Equal(maj23.Signature, key.Sign(metadata.ByteArray));
//             Assert.True(key.PublicKey.Verify(metadata.ByteArray, maj23.Signature));
//         }
//     }
// }
