using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;

namespace Libplanet.Tests.Consensus
{
    public class VoteMetadataTest
    {

        [Fact]
        public void NullBlockHashNotAllowedForNullAndUnknown()
        {
            var hash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));

            // Works with some hash value.
            _ = new VoteMetadata
            {
                Height = 2,
                Round = 2,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = new PrivateKey().PublicKey,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.Null,
            };
            _ = new VoteMetadata
            {
                Height = 2,
                Round = 2,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = new PrivateKey().PublicKey,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.Unknown,
            };

            // Null hash is not allowed.
            Assert.Throws<ArgumentException>(() => new VoteMetadata
            {
                Height = 2,
                Round = 2,
                BlockHash = default,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = new PrivateKey().PublicKey,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.Null,
            });
            Assert.Throws<ArgumentException>(() => new VoteMetadata
            {
                Height = 2,
                Round = 2,
                BlockHash = default,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = new PrivateKey().PublicKey,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.Unknown,
            });
        }

        [Fact]
        public void Bencoded()
        {
            var hash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            var key = new PrivateKey();
            var expected = new VoteMetadata
            {
                Height = 1,
                Round = 2,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = key.PublicKey,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            };
            var decoded = ModelSerializer.Deserialize<VoteMetadata>(
                ModelSerializer.Serialize(expected));
            Assert.Equal(expected, decoded);

            expected = new VoteMetadata
            {
                Height = 1,
                Round = 2,
                BlockHash = hash,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = key.PublicKey,
                Flag = VoteFlag.PreCommit,
            };
            decoded = ModelSerializer.Deserialize<VoteMetadata>(
                ModelSerializer.Serialize(expected));
            Assert.Equal(expected, decoded);
        }
    }
}
