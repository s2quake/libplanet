using System.Numerics;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
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
                    new VoteMetadata(
                        1,
                        0,
                        randomHash,
                        DateTimeOffset.UtcNow,
                        key.PublicKey,
                        index == 0 ? (BigInteger?)null : BigInteger.One,
                        VoteFlag.PreCommit).Sign(key))
                .ToImmutableArray();
            var blockCommit = new BlockCommit(1, 0, randomHash, votes);

            var commitHash = blockCommit.ToHash();
            var expected = HashDigest<SHA256>.DeriveFrom(_codec.Encode(blockCommit.Bencoded));

            Assert.Equal(commitHash, expected);
        }

        [Fact]
        public void HeightAndRoundMustNotBeNegative()
        {
            var hash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            var key = new PrivateKey();
            var votes = ImmutableArray<Vote>.Empty
                .Add(
                    new VoteMetadata(
                            0,
                            0,
                            hash,
                            DateTimeOffset.UtcNow,
                            key.PublicKey,
                            BigInteger.One,
                            VoteFlag.PreCommit)
                        .Sign(key));

            // Negative height is not allowed.
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BlockCommit(-1, 0, hash, votes));

            // Negative round is not allowed.
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BlockCommit(0, -1, hash, votes));
        }

        [Fact]
        public void VotesCannotBeEmpty()
        {
            var hash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            Assert.Throws<ArgumentException>(() =>
                new BlockCommit(0, 0, hash, default));
            Assert.Throws<ArgumentException>(() =>
                new BlockCommit(0, 0, hash, ImmutableArray<Vote>.Empty));
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
                .Add(new VoteMetadata(
                    height + 1,
                    round,
                    hash,
                    DateTimeOffset.UtcNow,
                    key.PublicKey,
                    BigInteger.One,
                    VoteFlag.PreCommit).Sign(key));
            Assert.Throws<ArgumentException>(() =>
                new BlockCommit(height, round, hash, votes));

            // Vote with different round is not allowed.
            votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata(
                    height,
                    round + 1,
                    hash,
                    DateTimeOffset.UtcNow,
                    key.PublicKey,
                    BigInteger.One,
                    VoteFlag.PreCommit).Sign(key));
            Assert.Throws<ArgumentException>(() =>
                new BlockCommit(height, round, hash, votes));
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
                .Add(new VoteMetadata(
                    height,
                    round,
                    badHash,
                    DateTimeOffset.UtcNow,
                    key.PublicKey,
                    BigInteger.One,
                    VoteFlag.PreCommit).Sign(key));
            Assert.Throws<ArgumentException>(() => new BlockCommit(height, round, hash, votes));
        }

        [Fact]
        public void EveryVoteFlagMustBeNullOrPreCommit()
        {
            var height = 2;
            var round = 3;
            var hash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
            var keys = Enumerable.Range(0, 4).Select(_ => new PrivateKey()).ToList();
            var preCommitVotes = keys.Select(
                    key => new VoteMetadata(
                            height,
                            round,
                            hash,
                            DateTimeOffset.UtcNow,
                            key.PublicKey,
                            BigInteger.One,
                            VoteFlag.PreCommit)
                        .Sign(key))
                .ToList();

            var votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata(
                    height,
                    round,
                    hash,
                    DateTimeOffset.UtcNow,
                    keys[0].PublicKey,
                    BigInteger.One,
                    VoteFlag.Null).Sign(null))
                .AddRange(preCommitVotes.Skip(1));
            _ = new BlockCommit(height, round, hash, votes);

            votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata(
                    height,
                    round,
                    hash,
                    DateTimeOffset.UtcNow,
                    keys[0].PublicKey,
                    BigInteger.One,
                    VoteFlag.Unknown).Sign(null))
                .AddRange(preCommitVotes.Skip(1));
            Assert.Throws<ArgumentException>(() => new BlockCommit(height, round, hash, votes));

            votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata(
                    height,
                    round,
                    hash,
                    DateTimeOffset.UtcNow,
                    keys[0].PublicKey,
                    BigInteger.One,
                    VoteFlag.PreVote).Sign(keys[0]))
                .AddRange(preCommitVotes.Skip(1));
            Assert.Throws<ArgumentException>(() => new BlockCommit(height, round, hash, votes));

            votes = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata(
                    height,
                    round,
                    hash,
                    DateTimeOffset.UtcNow,
                    keys[0].PublicKey,
                    BigInteger.One,
                    VoteFlag.PreCommit).Sign(keys[0]))
                .AddRange(preCommitVotes.Skip(1));
            _ = new BlockCommit(height, round, hash, votes);
        }

        [Fact]
        public void Bencoded()
        {
            var fx = new MemoryStoreFixture();
            var keys = Enumerable.Range(0, 4).Select(_ => new PrivateKey()).ToList();
            var votes = keys.Select(key =>
                new VoteMetadata(
                    1,
                    0,
                    fx.Hash1,
                    DateTimeOffset.Now,
                    key.PublicKey,
                    BigInteger.One,
                    VoteFlag.PreCommit).Sign(key))
                .ToImmutableArray();
            var expected = new BlockCommit(1, 0, fx.Hash1, votes);
            var decoded = new BlockCommit(expected.Bencoded);
            Assert.Equal(expected, decoded);
        }
    }
}
