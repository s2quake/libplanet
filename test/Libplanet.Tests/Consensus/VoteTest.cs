// using System.Numerics;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;
// using Xunit;

// namespace Libplanet.Tests.Consensus
// {
//     public class VoteTest
//     {
//         private static Bencodex.Codec _codec = new Bencodex.Codec();

//         [Fact]
//         public void Sign()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
//             var privateKey = new PrivateKey();
//             var voteMetadata = new VoteMetadata(
//                 1,
//                 2,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 privateKey.PublicKey,
//                 BigInteger.One,
//                 VoteType.PreCommit);
//             Vote vote = voteMetadata.Sign(privateKey);
//             Assert.True(
//                 privateKey.PublicKey.Verify(
//                     _codec.Encode(voteMetadata.Bencoded), vote.Signature.AsSpan()));

//             var nullPowerVoteMetadata = new VoteMetadata(
//                 1,
//                 2,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 privateKey.PublicKey,
//                 null,
//                 VoteType.PreCommit);
//             Vote nullPowerVote = nullPowerVoteMetadata.Sign(privateKey);
//             Assert.True(
//                 privateKey.PublicKey.Verify(
//                     _codec.Encode(nullPowerVoteMetadata.Bencoded),
//                     nullPowerVote.Signature.AsSpan()));
//         }

//         [Fact]
//         public void CannotSignWithWrongPrivateKey()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
//             var validatorPublicKey = new PrivateKey().PublicKey;
//             var key = new PrivateKey();
//             var voteMetadata = new VoteMetadata(
//                 height: 2,
//                 round: 3,
//                 blockHash: hash,
//                 timestamp: DateTimeOffset.UtcNow,
//                 validatorPublicKey: validatorPublicKey,
//                 validatorPower: BigInteger.One,
//                 flag: VoteType.PreCommit);

//             // Cannot sign with Sign method
//             Assert.Throws<ArgumentException>(() => voteMetadata.Sign(key));

//             // Cannot bypass by attaching signature
//             Assert.Throws<ArgumentException>(() =>
//                 new Vote(
//                     voteMetadata,
//                     key.Sign(_codec.Encode(voteMetadata.Bencoded).ToImmutableArray())));
//         }

//         [Fact]
//         public void EmptySignatureNotAllowedForPreVoteAndPreCommit()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
//             var key = new PrivateKey();
//             var preVoteMetadata = new VoteMetadata(
//                 height: 2,
//                 round: 3,
//                 blockHash: hash,
//                 timestamp: DateTimeOffset.UtcNow,
//                 validatorPublicKey: key.PublicKey,
//                 validatorPower: BigInteger.One,
//                 flag: VoteType.PreVote);
//             var preCommitMetadata = new VoteMetadata(
//                 height: 2,
//                 round: 3,
//                 blockHash: hash,
//                 timestamp: DateTimeOffset.UtcNow,
//                 validatorPublicKey: key.PublicKey,
//                 validatorPower: BigInteger.One,
//                 flag: VoteType.PreCommit);

//             // Works fine.
//             _ = preVoteMetadata.Sign(key);
//             _ = preCommitMetadata.Sign(key);

//             Assert.Throws<ArgumentException>(() => preVoteMetadata.Sign(null));
//             Assert.Throws<ArgumentException>(() =>
//                 new Vote(preVoteMetadata, ImmutableArray<byte>.Empty));
//             Assert.Throws<ArgumentException>(() => preCommitMetadata.Sign(null));
//             Assert.Throws<ArgumentException>(() =>
//                 new Vote(preCommitMetadata, ImmutableArray<byte>.Empty));
//         }

//         [Fact]
//         public void NonEmptySignatureNotAllowedForNullAndUnknown()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
//             var key = new PrivateKey();
//             var nullMetadata = new VoteMetadata(
//                 height: 2,
//                 round: 3,
//                 blockHash: hash,
//                 timestamp: DateTimeOffset.UtcNow,
//                 validatorPublicKey: key.PublicKey,
//                 validatorPower: BigInteger.One,
//                 flag: VoteType.Null);
//             var unknownMetadata = new VoteMetadata(
//                 height: 2,
//                 round: 3,
//                 blockHash: hash,
//                 timestamp: DateTimeOffset.UtcNow,
//                 validatorPublicKey: key.PublicKey,
//                 validatorPower: BigInteger.One,
//                 flag: VoteType.Unknown);

//             // Works fine.
//             _ = nullMetadata.Sign(null);
//             _ = unknownMetadata.Sign(null);

//             Assert.Throws<ArgumentException>(() => nullMetadata.Sign(key));
//             Assert.Throws<ArgumentException>(() =>
//                 new Vote(
//                     nullMetadata,
//                     key.Sign(_codec.Encode(nullMetadata.Bencoded)).ToImmutableArray()));
//             Assert.Throws<ArgumentException>(() => unknownMetadata.Sign(key));
//             Assert.Throws<ArgumentException>(() =>
//                 new Vote(
//                     unknownMetadata,
//                     key.Sign(_codec.Encode(unknownMetadata.Bencoded)).ToImmutableArray()));
//         }

//         [Fact]
//         public void DefaultSignatureIsInvalid()
//         {
//             var voteMetadata = new VoteMetadata(
//                 0,
//                 0,
//                 default,
//                 DateTimeOffset.UtcNow,
//                 new PrivateKey().PublicKey,
//                 BigInteger.One,
//                 VoteType.PreCommit);
//             Assert.Throws<ArgumentException>(() => new Vote(voteMetadata, default));
//         }

//         [Fact]
//         public void Bencoded()
//         {
//             var hash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));
//             var key = new PrivateKey();
//             var expected = new VoteMetadata(
//                 1,
//                 2,
//                 hash,
//                 DateTimeOffset.UtcNow,
//                 key.PublicKey,
//                 BigInteger.One,
//                 VoteType.PreCommit).Sign(key);
//             var decoded = new Vote(expected.Bencoded);
//             Assert.Equal(expected, decoded);
//         }
//     }
// }
