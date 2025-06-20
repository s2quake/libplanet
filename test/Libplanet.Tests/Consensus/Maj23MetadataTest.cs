// using Libplanet.Net.Consensus;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;

// namespace Libplanet.Tests.Consensus
// {
//     public class Maj23MetadataTest
//     {

//         [Fact]
//         public void VoteTypeShouldBePreVoteOrPreCommit()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));

//             // Works with PreVote and PreCommit vote flags.
//             _ = new Maj23Metadata(
//                 2, 2, hash, DateTimeOffset.UtcNow, new PrivateKey().PublicKey, VoteType.PreVote);
//             _ = new Maj23Metadata(
//                 2, 2, hash, DateTimeOffset.UtcNow, new PrivateKey().PublicKey, VoteType.PreCommit);

//             // Null and Unknown vote flags are not allowed.
//             Assert.Throws<ArgumentException>(() => new Maj23Metadata(
//                 2,
//                 2,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 new PrivateKey().PublicKey,
//                 VoteType.Null));
//             Assert.Throws<ArgumentException>(() => new Maj23Metadata(
//                 2,
//                 2,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 new PrivateKey().PublicKey,
//                 VoteType.Unknown));
//         }

//         [Fact]
//         public void Bencoded()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
//             var key = new PrivateKey();
//             var expected = new Maj23Metadata(
//                 1,
//                 2,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 key.PublicKey,
//                 VoteType.PreCommit);
//             var decoded = new Maj23Metadata(expected.Encoded);
//             Assert.Equal(expected, decoded);
//         }
//     }
// }
