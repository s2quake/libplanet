using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public partial class Context
{
    private void StartRound(int round)
    {
        Round = round;
        RoundStarted?.Invoke(this, Round);
        _heightVoteSet.SetRound(round);

        Proposal = null;
        Step = ConsensusStep.Propose;
        if (_validators.GetProposer(Height, Round).Address == _signer.Address)
        {
            if ((_validValue ?? GetValue()) is Block proposalValue)
            {
                var proposal = new ProposalMetadata
                {
                    Height = Height,
                    Round = Round,
                    Timestamp = DateTimeOffset.UtcNow,
                    Proposer = _signer.Address,
                    // MarshaledBlock = ModelSerializer.SerializeToBytes(proposalValue),
                    ValidRound = _validRound,
                }.Sign(_signer);

                PublishMessage(new ConsensusProposalMessage { Proposal = proposal });
            }
            else
            {
                // logging
                _ = OnTimeoutPropose(Round);
            }
        }
        else
        {
            // logging
            _ = OnTimeoutPropose(Round);
        }
    }

    /// <summary>
    /// Validates given <paramref name="message"/> and add it to the message log.
    /// </summary>
    /// <param name="message">A <see cref="ConsensusMessage"/> to be added.
    /// </param>
    /// <remarks>
    /// If an invalid <see cref="ConsensusMessage"/> is given, this method throws
    /// an <see cref="InvalidOperationException"/> and handles it <em>internally</em>
    /// while invoking <see cref="ExceptionOccurred"/> event.
    /// An <see cref="InvalidOperationException"/> can be thrown when
    /// the internal <see cref="HeightVoteSet"/> does not accept it, i.e.
    /// <see cref="HeightVoteSet.AddVote"/> returns <see langword="false"/>.
    /// </remarks>
    /// <seealso cref="HeightVoteSet.AddVote"/>
    private bool AddMessage(ConsensusMessage message)
    {
        try
        {
            if (message.Height != Height)
            {
                throw new InvalidOperationException(
                    $"Given message's height {message.Height} is invalid");
            }

            if (!_validators.Contains(message.Validator))
            {
                throw new InvalidOperationException(
                    $"Given message's validator {message.Validator} is invalid");
            }

            if (message is ConsensusProposalMessage proposal)
            {
                AddProposal(proposal.Proposal);
            }

            if (message is ConsensusVoteMessage voteMsg)
            {
                switch (voteMsg)
                {
                    case ConsensusPreVoteMessage preVote:
                        {
                            _heightVoteSet.AddVote(preVote.PreVote);
                            var args = (preVote.Round, VoteFlag.PreVote,
                                _heightVoteSet.PreVotes(preVote.Round).GetAllVotes());
                            VoteSetModified?.Invoke(this, args);
                            break;
                        }

                    case ConsensusPreCommitMessage preCommit:
                        {
                            _heightVoteSet.AddVote(preCommit.PreCommit);
                            var args = (preCommit.Round, VoteFlag.PreCommit,
                                _heightVoteSet.PreCommits(preCommit.Round).GetAllVotes());
                            VoteSetModified?.Invoke(this, args);
                            break;
                        }
                }

                return true;
            }

            return false;
        }
        catch (InvalidProposalException ipe)
        {
            // var icme = new InvalidOperationException(
            //     ipe.Message,
            //     message);
            // var msg = $"Failed to add invalid message {message} to the " +
            //           $"{nameof(HeightVoteSet)}";
            // _logger.Error(icme, msg);
            ExceptionOccurred?.Invoke(this, ipe);
            return false;
        }
        catch (InvalidVoteException ive)
        {
            // var icme = new InvalidOperationException(
            //     ive.Message,
            //     message);
            // var msg = $"Failed to add invalid message {message} to the " +
            //           $"{nameof(HeightVoteSet)}";
            // _logger.Error(icme, msg);
            ExceptionOccurred?.Invoke(this, ive);
            return false;
        }
        catch (Exception icme)
        {
            // var msg = $"Failed to add invalid message {message} to the " +
            //           $"{nameof(HeightVoteSet)}";
            // _logger.Error(icme, msg);
            ExceptionOccurred?.Invoke(this, icme);
            return false;
        }
    }

    private void AddProposal(Proposal proposal)
    {
        if (!_validators.GetProposer(Height, Round)
                .Address.Equals(proposal.Validator))
        {
            throw new InvalidProposalException(
                $"Given proposal's proposer {proposal.Validator} is not the " +
                $"proposer for the current height {Height} and round {Round}",
                proposal);
        }

        if (proposal.Round != Round)
        {
            throw new InvalidProposalException(
                $"Given proposal's round {proposal.Round} does not match" +
                $" with the current round {Round}",
                proposal);
        }

        // Should check if +2/3 votes already collected and the proposal does not match
        if (_heightVoteSet.PreVotes(Round).TwoThirdsMajority(out var preVoteMaj23) &&
            !proposal.BlockHash.Equals(preVoteMaj23))
        {
            throw new InvalidProposalException(
                $"Given proposal's block hash {proposal.BlockHash} does not match" +
                $" with the collected +2/3 preVotes' block hash {preVoteMaj23}",
                proposal);
        }

        if (_heightVoteSet.PreCommits(Round).TwoThirdsMajority(out var preCommitMaj23) &&
            !proposal.BlockHash.Equals(preCommitMaj23))
        {
            throw new InvalidProposalException(
                $"Given proposal's block hash {proposal.BlockHash} does not match" +
                $" with the collected +2/3 preCommits' block hash {preCommitMaj23}",
                proposal);
        }

        if (Proposal is null)
        {
            Proposal = proposal;
        }
        else
        {
            throw new InvalidProposalException(
                $"Proposal already exists for height {Height} and round {Round}",
                proposal);
        }
    }

    /// <summary>
    /// Checks the current state to mutate <see cref="ConsensusStep"/> and/or schedule timeouts.
    /// </summary>
    private void ProcessGenericUponRules()
    {
        if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
        {
            return;
        }

        (Block Block, int ValidRound)? propose = GetProposal();
        if (propose is { } p1 &&
            p1.ValidRound == -1 &&
            Step == ConsensusStep.Propose)
        {
            if (IsValid(p1.Block) && (_lockedRound == -1 || _lockedValue == p1.Block))
            {
                EnterPreVote(Round, p1.Block.BlockHash);
            }
            else
            {
                EnterPreVote(Round, default);
            }
        }

        if (propose is { } p2 &&
            p2.ValidRound >= 0 &&
            p2.ValidRound < Round &&
            _heightVoteSet.PreVotes(p2.ValidRound).TwoThirdsMajority(out BlockHash hash1) &&
            hash1.Equals(p2.Block.BlockHash) &&
            Step == ConsensusStep.Propose)
        {
            if (IsValid(p2.Block) &&
                (_lockedRound <= p2.ValidRound || _lockedValue == p2.Block))
            {
                EnterPreVote(Round, p2.Block.BlockHash);
            }
            else
            {
                EnterPreVote(Round, default);
            }
        }

        if (_heightVoteSet.PreVotes(Round).HasTwoThirdsAny() && Step == ConsensusStep.PreVote)
        {
            _ = OnTimeoutPreVote(Round);
        }

        if (propose is { } p3 &&
            _heightVoteSet.PreVotes(Round).TwoThirdsMajority(out BlockHash hash2) &&
            hash2.Equals(p3.Block.BlockHash) &&
            IsValid(p3.Block) &&
            (Step == ConsensusStep.PreVote || Step == ConsensusStep.PreCommit) &&
            !_hasTwoThirdsPreVoteFlags.Contains(Round))
        {
            _hasTwoThirdsPreVoteFlags.Add(Round);
            if (Step == ConsensusStep.PreVote)
            {
                _lockedValue = p3.Block;
                _lockedRound = Round;
                _ = EnterPreCommitWait(Round, p3.Block.BlockHash);

                // Maybe need to broadcast periodically?
                PublishMessage(
                    new ConsensusMaj23Message
                    {
                        Maj23 = MakeMaj23(Round, p3.Block.BlockHash, VoteFlag.PreVote),
                    });
            }

            _validValue = p3.Block;
            _validRound = Round;
        }

        if (_heightVoteSet.PreVotes(Round).TwoThirdsMajority(out BlockHash hash3) &&
            Step == ConsensusStep.PreVote)
        {
            if (hash3.Equals(default))
            {
                _ = EnterPreCommitWait(Round, default);
            }
            else if (Proposal is { } proposal && !proposal.BlockHash.Equals(hash3))
            {
                // +2/3 votes were collected and is not equal to proposal's,
                // remove invalid proposal.
                Proposal = null;
                PublishMessage(
                    new ConsensusProposalClaimMessage
                    {
                        ProposalClaim = new ProposalClaimMetadata
                        {
                            Height = Height,
                            Round = Round,
                            BlockHash = hash3,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = _signer.Address,
                        }.Sign(_signer),
                    });
            }
        }

        if (_heightVoteSet.PreCommits(Round).HasTwoThirdsAny())
        {
            _ = OnTimeoutPreCommit(Round);
        }
    }

    /// <summary>
    /// Checks the current state to mutate <see cref="Round"/> or to terminate
    /// by setting <see cref="ConsensusStep"/> to <see cref="ConsensusStep.EndCommit"/>.
    /// </summary>
    /// <param name="message">The <see cref="ConsensusMessage"/> to process.
    /// Although this is not strictly needed, this is used for optimization.</param>
    private void ProcessHeightOrRoundUponRules(ConsensusMessage message)
    {
        if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
        {
            return;
        }

        int round = message.Round;
        if ((message is ConsensusProposalMessage || message is ConsensusPreCommitMessage) &&
            GetProposal() is (Block block4, _) &&
            _heightVoteSet.PreCommits(Round).TwoThirdsMajority(out BlockHash hash) &&
            block4.BlockHash.Equals(hash) &&
            IsValid(block4))
        {
            _decision = block4;
            _committedRound = round;

            // Maybe need to broadcast periodically?
            PublishMessage(
                new ConsensusMaj23Message
                {
                    Maj23 = MakeMaj23(round, block4.BlockHash, VoteFlag.PreCommit),
                });
            _ = EnterEndCommitWait(Round);
            return;
        }

        // NOTE: +1/3 prevote received, skip round
        // FIXME: Tendermint uses +2/3, should be fixed?
        if (round > Round &&
            _heightVoteSet.PreVotes(round).HasOneThirdsAny())
        {
            StartRound(round);
            return;
        }
    }

    private void EnterPreVote(int round, BlockHash blockHash)
    {
        if (Round != round || Step >= ConsensusStep.PreVote)
        {
            // Round and step mismatch
            return;
        }

        Step = ConsensusStep.PreVote;
        PublishMessage(
            new ConsensusPreVoteMessage { PreVote = MakeVote(round, blockHash, VoteFlag.PreVote) });
    }

    private void EnterPreCommit(int round, BlockHash hash)
    {
        if (Round != round || Step >= ConsensusStep.PreCommit)
        {
            // Round and step mismatch
            return;
        }

        Step = ConsensusStep.PreCommit;
        PublishMessage(
            new ConsensusPreCommitMessage { PreCommit = MakeVote(round, hash, VoteFlag.PreCommit) });
    }

    private void EnterEndCommit(int round)
    {
        if (Round != round ||
            Step == ConsensusStep.Default ||
            Step == ConsensusStep.EndCommit)
        {
            // Round and step mismatch
            return;
        }

        Step = ConsensusStep.EndCommit;
        if (_decision is not { } block)
        {
            StartRound(Round + 1);
            return;
        }

        try
        {
            IsValid(block);
            AppendBlock(block);
        }
        catch (Exception e)
        {
            ExceptionOccurred?.Invoke(this, e);
            return;
        }
    }

    private void ProcessTimeoutPropose(int round)
    {
        if (round == Round && Step == ConsensusStep.Propose)
        {
            EnterPreVote(round, default);
            TimeoutProcessed?.Invoke(this, (round, ConsensusStep.Propose));
        }
    }

    /// <summary>
    /// A timeout mutation to run if +2/3 <see cref="ConsensusPreVoteMessage"/>s were received but
    /// is still in <paramref name="round"/> round and <see cref="ConsensusStep.PreVote"/> step
    /// after <see cref="TimeoutPreVote"/>.
    /// </summary>
    /// <param name="round">A round that the timeout task is scheduled for.</param>
    private void ProcessTimeoutPreVote(int round)
    {
        if (round == Round && Step == ConsensusStep.PreVote)
        {
            EnterPreCommit(round, default);
            TimeoutProcessed?.Invoke(this, (round, ConsensusStep.PreVote));
        }
    }

    /// <summary>
    /// A timeout mutation to run if +2/3 <see cref="ConsensusPreCommitMessage"/>s were received but
    /// is still in <paramref name="round"/> round and <see cref="ConsensusStep.PreCommit"/>
    /// step after <see cref="TimeoutPreCommit"/>.
    /// </summary>
    /// <param name="round">A round that the timeout task is scheduled for.</param>
    private void ProcessTimeoutPreCommit(int round)
    {
        if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
        {
            return;
        }

        if (round == Round)
        {
            EnterEndCommit(round);
            TimeoutProcessed?.Invoke(this, (round, ConsensusStep.PreCommit));
        }
    }
}
