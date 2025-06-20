// using Libplanet.Serialization;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;

// namespace Libplanet.Tests.Consensus
// {
//     public class VoteMetadataTest
//     {

//         [Fact]
//         public void NullBlockHashNotAllowedForNullAndUnknown()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));

//             // Works with some hash value.
//             _ = new VoteMetadata
//             {
//                 Height = 2,
//                 Round = 2,
//                 BlockHash = hash,
//                 Timestamp = DateTimeOffset.UtcNow,
//                 Validator = new PrivateKey().PublicKey,
//                 ValidatorPower = BigInteger.One,
//                 Flag = VoteType.Null,
//             };
//             _ = new VoteMetadata
//             {
//                 Height = 2,
//                 Round = 2,
//                 BlockHash = hash,
//                 Timestamp = DateTimeOffset.UtcNow,
//                 Validator = new PrivateKey().PublicKey,
//                 ValidatorPower = BigInteger.One,
//                 Flag = VoteType.Unknown,
//             };

//             // Null hash is not allowed.
//             Assert.Throws<ArgumentException>(() => new VoteMetadata
//             {
//                 Height = 2,
//                 Round = 2,
//                 BlockHash = default,
//                 Timestamp = DateTimeOffset.UtcNow,
//                 Validator = new PrivateKey().PublicKey,
//                 ValidatorPower = BigInteger.One,
//                 Flag = VoteType.Null,
//             });
//             Assert.Throws<ArgumentException>(() => new VoteMetadata
//             {
//                 Height = 2,
//                 Round = 2,
//                 BlockHash = default,
//                 Timestamp = DateTimeOffset.UtcNow,
//                 Validator = new PrivateKey().PublicKey,
//                 ValidatorPower = BigInteger.One,
//                 Flag = VoteType.Unknown,
//             });
//         }

//         [Fact]
//         public void Bencoded()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
//             var key = new PrivateKey();
//             var expected = new VoteMetadata
//             {
//                 Height = 1,
//                 Round = 2,
//                 BlockHash = hash,
//                 Timestamp = DateTimeOffset.UtcNow,
//                 Validator = key.PublicKey,
//                 ValidatorPower = BigInteger.One,
//                 Flag = VoteType.PreCommit,
//             };
//             var decoded = ModelSerializer.Deserialize<VoteMetadata>(
//                 ModelSerializer.Serialize(expected));
//             Assert.Equal(expected, decoded);

//             expected = new VoteMetadata
//             {
//                 Height = 1,
//                 Round = 2,
//                 BlockHash = hash,
//                 Timestamp = DateTimeOffset.UtcNow,
//                 Validator = key.PublicKey,
//                 Flag = VoteType.PreCommit,
//             };
//             decoded = ModelSerializer.Deserialize<VoteMetadata>(
//                 ModelSerializer.Serialize(expected));
//             Assert.Equal(expected, decoded);
//         }
//     }
// }
