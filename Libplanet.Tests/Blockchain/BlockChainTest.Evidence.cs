using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Evidences;
using Xunit;

namespace Libplanet.Tests.Blockchain
{
    public partial class BlockChainTest
    {
        [Fact]
        public void GetEmptyPendingEvidences()
        {
            var emptyEvidences = _blockChain.GetPendingEvidences();
            Assert.Empty(emptyEvidences);
        }

        [Fact]
        public void ProcessDuplicatedVoteEvidence()
        {
            var key = TestUtils.ValidatorPrivateKeys.First();
            var voteRef = new VoteMetadata(
                height: _blockChain.Tip.Index,
                round: 2,
                blockHash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                timestamp: DateTimeOffset.UtcNow,
                validatorPublicKey: key.PublicKey,
                validatorPower: BigInteger.One,
                flag: VoteFlag.PreCommit).Sign(key);
            var voteDup = new VoteMetadata(
                height: _blockChain.Tip.Index,
                round: 2,
                blockHash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                timestamp: DateTimeOffset.UtcNow,
                validatorPublicKey: key.PublicKey,
                validatorPower: BigInteger.One,
                flag: VoteFlag.PreCommit).Sign(key);
            var evidence = new DuplicateVoteEvidence(
                voteRef,
                voteDup,
                TestUtils.ValidatorSet,
                voteDup.Timestamp);

            var duplicateVoteSets = new List<(Vote, Vote)>()
            {
                (voteRef, voteDup),
            };

            Assert.Empty(_blockChain.GetPendingEvidences());
            Assert.False(_blockChain.IsEvidencePending(evidence.Id));
            Assert.False(_blockChain.IsEvidenceCommitted(evidence.Id));

            Assert.Single(_blockChain.GetPendingEvidences());
            Assert.Equal(evidence, _blockChain.GetPendingEvidences().First());
            Assert.True(_blockChain.IsEvidencePending(evidence.Id));
            Assert.False(_blockChain.IsEvidenceCommitted(evidence.Id));

            _blockChain.CommitEvidence(evidence);

            Assert.Empty(_blockChain.GetPendingEvidences());
            Assert.False(_blockChain.IsEvidencePending(evidence.Id));
            Assert.True(_blockChain.IsEvidenceCommitted(evidence.Id));
        }

        [Fact]
        public void CommitEvidence()
        {
            var key = TestUtils.ValidatorPrivateKeys.First();
            var voteRef = new VoteMetadata(
                height: _blockChain.Tip.Index,
                round: 2,
                blockHash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                timestamp: DateTimeOffset.UtcNow,
                validatorPublicKey: key.PublicKey,
                validatorPower: BigInteger.One,
                flag: VoteFlag.PreCommit).Sign(key);
            var voteDup = new VoteMetadata(
                height: _blockChain.Tip.Index,
                round: 2,
                blockHash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                timestamp: DateTimeOffset.UtcNow,
                validatorPublicKey: key.PublicKey,
                validatorPower: BigInteger.One,
                flag: VoteFlag.PreCommit).Sign(key);
            var evidence = new DuplicateVoteEvidence(
                voteRef,
                voteDup,
                TestUtils.ValidatorSet,
                voteDup.Timestamp);

            Assert.Empty(_blockChain.GetPendingEvidences());
            Assert.False(_blockChain.IsEvidencePending(evidence.Id));
            Assert.False(_blockChain.IsEvidenceCommitted(evidence.Id));

            _blockChain.ProcessEvidenceExpirationAhead();
            _blockChain.CommitEvidence(evidence);

            Assert.Empty(_blockChain.GetPendingEvidences());
            Assert.False(_blockChain.IsEvidencePending(evidence.Id));
            Assert.True(_blockChain.IsEvidenceCommitted(evidence.Id));
        }

        [Fact]
        public void CannotAddDuplicatedVoteEvidenceFromNonValidator()
        {
            var key = new PrivateKey();
            var voteRef = new VoteMetadata(
                height: _blockChain.Tip.Index,
                round: 2,
                blockHash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                timestamp: DateTimeOffset.UtcNow,
                validatorPublicKey: key.PublicKey,
                validatorPower: BigInteger.One,
                flag: VoteFlag.PreCommit).Sign(key);
            var voteDup = new VoteMetadata(
                height: _blockChain.Tip.Index,
                round: 2,
                blockHash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                timestamp: DateTimeOffset.UtcNow,
                validatorPublicKey: key.PublicKey,
                validatorPower: BigInteger.One,
                flag: VoteFlag.PreCommit).Sign(key);
            var evidence = new DuplicateVoteEvidence(
                voteRef,
                voteDup,
                new ValidatorSet(
                    new List<Validator> { new Validator(key.PublicKey, BigInteger.One) }),
                voteDup.Timestamp);

            var duplicatedVoteSets = new List<(Vote, Vote)>()
            {
                (voteRef, voteDup),
            };

            Assert.Empty(_blockChain.GetPendingEvidences());
            Assert.False(_blockChain.IsEvidencePending(evidence.Id));
            Assert.False(_blockChain.IsEvidenceCommitted(evidence.Id));

            Assert.Empty(_blockChain.GetPendingEvidences());
            Assert.False(_blockChain.IsEvidencePending(evidence.Id));
            Assert.False(_blockChain.IsEvidenceCommitted(evidence.Id));
        }

        [Fact]
        public void ExpireEvidence()
        {
            var key = TestUtils.ValidatorPrivateKeys.First();
            var voteRef = new VoteMetadata(
                height: _blockChain.Tip.Index,
                round: 2,
                blockHash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                timestamp: DateTimeOffset.UtcNow,
                validatorPublicKey: key.PublicKey,
                validatorPower: BigInteger.One,
                flag: VoteFlag.PreCommit).Sign(key);
            var voteDup = new VoteMetadata(
                height: _blockChain.Tip.Index,
                round: 2,
                blockHash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                timestamp: DateTimeOffset.UtcNow,
                validatorPublicKey: key.PublicKey,
                validatorPower: BigInteger.One,
                flag: VoteFlag.PreCommit).Sign(key);
            var evidence = new DuplicateVoteEvidence(
                voteRef,
                voteDup,
                TestUtils.ValidatorSet,
                voteDup.Timestamp);

            var duplicatedVoteSets = new List<(Vote, Vote)>()
            {
                (voteRef, voteDup),
            };

            Assert.Empty(_blockChain.GetPendingEvidences());
            Assert.False(_blockChain.IsEvidencePending(evidence.Id));
            Assert.False(_blockChain.IsEvidenceCommitted(evidence.Id));

            Assert.Single(_blockChain.GetPendingEvidences());
            Assert.Equal(evidence, _blockChain.GetPendingEvidences().First());
            Assert.True(_blockChain.IsEvidencePending(evidence.Id));
            Assert.False(_blockChain.IsEvidenceCommitted(evidence.Id));

            var dur = _blockChain.Policy.GetMaxEvidencePendingDuration(_blockChain.Tip.Index);

            for (int i = 0; i < dur; i++)
            {
                var block = _blockChain.ProposeBlock(
                    key,
                    TestUtils.CreateBlockCommit(_blockChain.Tip, true),
                    _blockChain.GetPendingEvidences());
                _blockChain.Append(block, TestUtils.CreateBlockCommit(block, true));
            }

            var expiredEvidenceBlock = _blockChain.ProposeBlock(
                key,
                TestUtils.CreateBlockCommit(_blockChain.Tip, true),
                _blockChain.GetPendingEvidences());
            Assert.Throws<InvalidBlockEvidencesPendingDurationException>(
                () => _blockChain.Append(
                    expiredEvidenceBlock,
                    TestUtils.CreateBlockCommit(expiredEvidenceBlock, true)));

            _blockChain.ProcessEvidenceExpirationAhead();
            Assert.Empty(_blockChain.GetPendingEvidences());
        }
    }
}
