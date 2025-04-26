using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Net.Consensus;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Evidence;
using Xunit;

namespace Libplanet.Net.Tests.Consensus
{
    public class HeightVoteSetTest
    {
        private BlockChain _blockChain;
        private BlockCommit _lastCommit;
        private HeightVoteSet _heightVoteSet;

        /// <summary>
        /// Sets up a <see cref="BlockChain"/> with tip index of 1, i.e. two blocks.
        /// </summary>
        public HeightVoteSetTest()
        {
            _blockChain = TestUtils.CreateDummyBlockChain();
            var block = _blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            _lastCommit = TestUtils.CreateBlockCommit(block);
            _heightVoteSet = new HeightVoteSet(2, TestUtils.Validators);
            _blockChain.Append(block, TestUtils.CreateBlockCommit(block));
        }

        [Fact]
        public void CannotAddDifferentHeight()
        {
            var preVote = new VoteMetadata
            {
                Height = 3,
                Round = 0,
                BlockHash = default,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = TestUtils.PrivateKeys[0].PublicKey,
                ValidatorPower = TestUtils.Validators[0].Power,
                Flag = VoteFlag.PreVote,
            }.Sign(TestUtils.PrivateKeys[0]);

            Assert.Throws<InvalidVoteException>(() => _heightVoteSet.AddVote(preVote));
        }

        [Fact]
        public void CannotAddUnknownValidator()
        {
            var key = new PrivateKey();
            var preVote = new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = default,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = key.PublicKey,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreVote,
            }.Sign(key);

            Assert.Throws<InvalidVoteException>(() => _heightVoteSet.AddVote(preVote));
        }

        [Fact]
        public void CannotAddValidatorWithInvalidPower()
        {
            var preVote = new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = default,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = TestUtils.Validators[0].PublicKey,
                ValidatorPower = TestUtils.Validators[0].Power + 1,
                Flag = VoteFlag.PreVote,
            }.Sign(TestUtils.PrivateKeys[0]);

            Assert.Throws<InvalidVoteException>(() => _heightVoteSet.AddVote(preVote));
        }

        [Fact]
        public void CannotAddMultipleVotesPerRoundPerValidator()
        {
            Random random = new Random();
            var preVote0 = new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = default,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = TestUtils.PrivateKeys[0].PublicKey,
                ValidatorPower = TestUtils.Validators[0].Power,
                Flag = VoteFlag.PreVote,
            }.Sign(TestUtils.PrivateKeys[0]);
            var preVote1 = new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = TestUtils.PrivateKeys[0].PublicKey,
                ValidatorPower = TestUtils.Validators[0].Power,
                Flag = VoteFlag.PreVote,
            }.Sign(TestUtils.PrivateKeys[0]);
            var preCommit0 = new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = default,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = TestUtils.PrivateKeys[0].PublicKey,
                ValidatorPower = TestUtils.Validators[0].Power,
                Flag = VoteFlag.PreCommit,
            }.Sign(TestUtils.PrivateKeys[0]);
            var preCommit1 = new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = TestUtils.PrivateKeys[0].PublicKey,
                ValidatorPower = TestUtils.Validators[0].Power,
                Flag = VoteFlag.PreCommit,
            }.Sign(TestUtils.PrivateKeys[0]);

            _heightVoteSet.AddVote(preVote0);
            Assert.Throws<DuplicateVoteException>(() => _heightVoteSet.AddVote(preVote1));
            _heightVoteSet.AddVote(preCommit0);
            Assert.Throws<DuplicateVoteException>(
                () => _heightVoteSet.AddVote(preCommit1));
        }

        [Fact]
        public void CannotAddVoteWithoutValidatorPower()
        {
            var preVote = new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = default,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = TestUtils.PrivateKeys[0].PublicKey,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreVote,
            }.Sign(TestUtils.PrivateKeys[0]);

            var exception = Assert.Throws<InvalidVoteException>(
                () => _heightVoteSet.AddVote(preVote));
            Assert.Equal("ValidatorPower of the vote cannot be null", exception.Message);
        }

        [Fact]
        public void GetCount()
        {
            var preVotes = Enumerable.Range(0, TestUtils.PrivateKeys.Count)
                .Select(
                    index => new VoteMetadata
                    {
                        Height = 2,
                        Round = 0,
                        BlockHash = default,
                        Timestamp = DateTimeOffset.UtcNow,
                        ValidatorPublicKey = TestUtils.PrivateKeys[index].PublicKey,
                        ValidatorPower = TestUtils.Validators[index].Power,
                        Flag = VoteFlag.PreVote,
                    }.Sign(TestUtils.PrivateKeys[index]))
                .ToList();
            var preCommit = new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = default,
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorPublicKey = TestUtils.PrivateKeys[0].PublicKey,
                ValidatorPower = TestUtils.Validators[0].Power,
                Flag = VoteFlag.PreCommit,
            }.Sign(TestUtils.PrivateKeys[0]);

            foreach (var preVote in preVotes)
            {
                _heightVoteSet.AddVote(preVote);
            }

            _heightVoteSet.AddVote(preCommit);

            Assert.Equal(5, _heightVoteSet.Count);
        }
    }
}
