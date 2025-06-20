// using Libplanet.Net.Consensus;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;

// namespace Libplanet.Tests.Consensus
// {
//     public class VoteSetBitsMetadataTest
//     {
//         [Fact]
//         public void Bencoded()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
//             var key = new PrivateKey();
//             var voteBits = new[] { true, true, false, false };
//             var expected = new VoteSetBitsMetadata(
//                 1,
//                 2,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 key.PublicKey,
//                 VoteType.PreCommit,
//                 voteBits);
//             var decoded = new VoteSetBitsMetadata(expected.Encoded);
//             Assert.Equal(expected, decoded);
//         }
//     }
// }
