using System.Numerics;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Tests.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Xunit;
using Xunit.Abstractions;

namespace Libplanet.Tests.Blocks
{
    public class BlockCommitTest
    {
        private static readonly Bencodex.Codec _codec = new Bencodex.Codec();
        private readonly ITestOutputHelper _output;

        public BlockCommitTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ToHash()
        {
            var randomHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            var keys = Enumerable.Range(0, 4).Select(_ => new PrivateKey()).ToList();
            var votes = keys.Select((key, index) =>
                    new VoteMetadata
                    {
                        Height = 1,
                        Round = 0,
                        BlockHash = randomHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        ValidatorPublicKey = key.PublicKey,
                        ValidatorPower = index == 0 ? BigInteger.Zero : BigInteger.One,
                        Flag = VoteFlag.PreCommit,
                    }.Sign(key))
                .ToImmutableArray();
            var blockCommit = new BlockCommit
            {
                Height = 1,
                Round = 0,
                BlockHash = randomHash,
                Votes = votes,
            };

            var commitHash = blockCommit.ToHash();
            var expected = HashDigest<SHA256>.DeriveFrom(ModelSerializer.SerializeToBytes(blockCommit));

            Assert.Equal(commitHash, expected);
        }

        [Fact]
        public void HeightAndRoundMustNotBeNegative()
        {
            var hash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            var key = new PrivateKey();
            var votes = ImmutableArray<Vote>.Empty
                .Add(
                    new VoteMetadata
                    {
                        Height = 0,
                        Round = 0,
                        BlockHash = hash,
                        Timestamp = DateTimeOffset.UtcNow,
                        ValidatorPublicKey = key.PublicKey,
                        ValidatorPower = BigInteger.One,
                        Flag = VoteFlag.PreCommit,
                    }.Sign(key));

            // Negative height is not allowed.
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BlockCommit
                {
                    Height = -1,
                    Round = 0,
                    BlockHash = hash,
                    Votes = votes,
                });

            // Negative round is not allowed.
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BlockCommit
                {
                    Height = 0,
                    Round = -1,
                    BlockHash = hash,
                    Votes = votes,
                });
        }

        [Fact]
        public void VotesCannotBeEmpty()
        {
            var hash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            Assert.Throws<ArgumentException>(() =>
                new BlockCommit
                {
                    Height = 0,
                    Round = 0,
                    BlockHash = hash,
                    Votes = default,
                });
            Assert.Throws<ArgumentException>(() =>
                new BlockCommit
                {
                    Height = 0,
                    Round = 0,
                    BlockHash = hash,
                    Votes = [],
                });
        }

        [Fact]
        public void EveryVoteMustHaveSameHeightAndRoundAsBlockCommit()
        {
            var height = 2;
            var round = 3;
            var hash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            var key = new PrivateKey();

            // Vote with different height is not allowed.
            var votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = height + 1,
                    Round = round,
                    BlockHash = hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = key.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(key));
            Assert.Throws<ArgumentException>(() =>
                new BlockCommit
                {
                    Height = height,
                    Round = round,
                    BlockHash = hash,
                    Votes = votes,
                });

            // Vote with different round is not allowed.
            votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = height,
                    Round = round + 1,
                    BlockHash = hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = key.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(key));
            Assert.Throws<ArgumentException>(() =>
                new BlockCommit
                {
                    Height = height,
                    Round = round,
                    BlockHash = hash,
                    Votes = votes,
                });
        }

        [Fact]
        public void EveryVoteMustHaveSameHashAsBlockCommit()
        {
            var height = 2;
            var round = 3;
            var hash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            var badHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            var key = new PrivateKey();

            var votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = height,
                    Round = round,
                    BlockHash = badHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = key.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(key));
            Assert.Throws<ArgumentException>(() => new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Votes = votes,
            });
        }

        [Fact]
        public void EveryVoteFlagMustBeNullOrPreCommit()
        {
            var height = 2;
            var round = 3;
            var hash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            var keys = Enumerable.Range(0, 4).Select(_ => new PrivateKey()).ToList();
            var preCommitVotes = keys.Select(
                    key => new VoteMetadata
                    {
                        Height = height,
                        Round = round,
                        BlockHash = hash,
                        Timestamp = DateTimeOffset.UtcNow,
                        ValidatorPublicKey = key.PublicKey,
                        ValidatorPower = BigInteger.One,
                        Flag = VoteFlag.PreCommit,
                    }.Sign(key))
                .ToList();

            var votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = height,
                    Round = round,
                    BlockHash = hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = keys[0].PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.Null,
                }.Sign(null))
                .AddRange(preCommitVotes.Skip(1));
            _ = new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Votes = votes,
            };

            votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = height,
                    Round = round,
                    BlockHash = hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = keys[0].PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.Unknown,
                }.Sign(null))
                .AddRange(preCommitVotes.Skip(1));
            Assert.Throws<ArgumentException>(() => new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Votes = votes,
            });

            votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = height,
                    Round = round,
                    BlockHash = hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = keys[0].PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreVote,
                }.Sign(keys[0]))
                .AddRange(preCommitVotes.Skip(1));
            Assert.Throws<ArgumentException>(() => new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Votes = votes,
            });

            votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = height,
                    Round = round,
                    BlockHash = hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = keys[0].PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(keys[0]))
                .AddRange(preCommitVotes.Skip(1));
            _ = new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Votes = votes,
            };
        }

        [Fact]
        public void Bencoded()
        {
            var fx = new MemoryStoreFixture();
            var keys = Enumerable.Range(0, 4).Select(_ => new PrivateKey()).ToList();
            var votes = keys.Select(key =>
                new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = fx.Hash1,
                    Timestamp = DateTimeOffset.Now,
                    ValidatorPublicKey = key.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(key))
                .ToImmutableArray();
            var expected = new BlockCommit
            {
                Height = 1,
                Round = 0,
                BlockHash = fx.Hash1,
                Votes = votes,
            };
            var decoded = ModelSerializer.Deserialize<BlockCommit>(ModelSerializer.Serialize(expected));
            Assert.Equal(expected, decoded);
        }
    }
}
