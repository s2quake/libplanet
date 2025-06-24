using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public partial class Context
{
    private void StartRound(int round)
    {
        Round = round;
        _roundStartedSubject.OnNext(round);
        _heightVotes.SetRound(round);

        Proposal = null;
        Step = ConsensusStep.Propose;
        if (_validators.GetProposer(Height, Round).Address == _signer.Address)
        {
            if ((_validBlock ?? GetValue()) is Block proposalValue)
            {
                var proposal = new ProposalMetadata
                {
                    Height = Height,
                    Round = Round,
                    Timestamp = DateTimeOffset.UtcNow,
                    Proposer = _signer.Address,
                    ValidRound = _validRound,
                }.Sign(_signer, proposalValue);

                _messagePublishedSubject.OnNext(new ConsensusProposalMessage { Proposal = proposal });
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
                            _heightVotes.AddVote(preVote.PreVote);
                            var args = (preVote.Round, VoteType.PreVote,
                                _heightVotes.PreVotes(preVote.Round).GetAllVotes());
                            VoteSetModified?.Invoke(this, args);
                            break;
                        }

                    case ConsensusPreCommitMessage preCommit:
                        {
                            _heightVotes.AddVote(preCommit.PreCommit);
                            var args = (preCommit.Round, VoteType.PreCommit,
                                _heightVotes.PreCommits(preCommit.Round).GetAllVotes());
                            VoteSetModified?.Invoke(this, args);
                            break;
                        }
                }

                return true;
            }

            return false;
        }
        catch (Exception icme)
        {
            _exceptionOccurredSubject.OnNext(icme);
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
        if (_heightVotes.PreVotes(Round).TwoThirdsMajority(out var preVoteMaj23) &&
            !proposal.BlockHash.Equals(preVoteMaj23))
        {
            throw new InvalidProposalException(
                $"Given proposal's block hash {proposal.BlockHash} does not match" +
                $" with the collected +2/3 preVotes' block hash {preVoteMaj23}",
                proposal);
        }

        if (_heightVotes.PreCommits(Round).TwoThirdsMajority(out var preCommitMaj23) &&
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

    private void ProcessGenericUponRules()
    {
        if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
        {
            return;
        }

        (Block Block, int ValidRound)? propose = GetProposal();
        if (Step == ConsensusStep.Propose && propose is { } p1 && p1.ValidRound == -1)
        {
            if (IsValid(p1.Block) && (_lockedRound == -1 || _lockedBlock == p1.Block))
            {
                EnterPreVote(Round, p1.Block.BlockHash);
            }
            else
            {
                EnterPreVote(Round, default);
            }
        }

        if (Step == ConsensusStep.Propose
            && propose is { } p2
            && p2.ValidRound >= 0
            && p2.ValidRound < Round
            && _heightVotes.PreVotes(p2.ValidRound).TwoThirdsMajority(out BlockHash hash1)
            && hash1.Equals(p2.Block.BlockHash))
        {
            if (IsValid(p2.Block) && (_lockedRound <= p2.ValidRound || _lockedBlock == p2.Block))
            {
                EnterPreVote(Round, p2.Block.BlockHash);
            }
            else
            {
                EnterPreVote(Round, default);
            }
        }

        if (Step == ConsensusStep.PreVote && _heightVotes.PreVotes(Round).HasTwoThirdsAny)
        {
            _ = OnTimeoutPreVote(Round);
        }

        if ((Step == ConsensusStep.PreVote || Step == ConsensusStep.PreCommit)
            && propose is { } p3
            && _heightVotes.PreVotes(Round).TwoThirdsMajority(out BlockHash hash2)
            && hash2.Equals(p3.Block.BlockHash)
            && IsValid(p3.Block)
            && !_hasTwoThirdsPreVoteTypes.Contains(Round))
        {
            _hasTwoThirdsPreVoteTypes.Add(Round);
            if (Step == ConsensusStep.PreVote)
            {
                _lockedBlock = p3.Block;
                _lockedRound = Round;
                _ = EnterPreCommitWait(Round, p3.Block.BlockHash, default);

                // Maybe need to broadcast periodically?
                _messagePublishedSubject.OnNext(
                    new ConsensusMaj23Message
                    {
                        Maj23 = MakeMaj23(Round, p3.Block.BlockHash, VoteType.PreVote),
                    });
            }

            _validBlock = p3.Block;
            _validRound = Round;
        }

        if (Step == ConsensusStep.PreVote
            && _heightVotes.PreVotes(Round).TwoThirdsMajority(out BlockHash hash3))
        {
            if (hash3.Equals(default))
            {
                _ = EnterPreCommitWait(Round, default, default);
            }
            else if (Proposal is { } proposal && !proposal.BlockHash.Equals(hash3))
            {
                // +2/3 votes were collected and is not equal to proposal's,
                // remove invalid proposal.
                Proposal = null;
                _messagePublishedSubject.OnNext(
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

        if (_heightVotes.PreCommits(Round).HasTwoThirdsAny)
        {
            _ = OnTimeoutPreCommit(Round);
        }
    }

    private void ProcessHeightOrRoundUponRules(ConsensusMessage message)
    {
        if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
        {
            return;
        }

        var round = message.Round;
        if ((message is ConsensusProposalMessage || message is ConsensusPreCommitMessage) &&
            GetProposal() is (Block block4, _) &&
            _heightVotes.PreCommits(Round).TwoThirdsMajority(out BlockHash hash) &&
            block4.BlockHash.Equals(hash) &&
            IsValid(block4))
        {
            _decision = block4;
            _committedRound = round;

            // Maybe need to broadcast periodically?
            _messagePublishedSubject.OnNext(
                new ConsensusMaj23Message
                {
                    Maj23 = MakeMaj23(round, block4.BlockHash, VoteType.PreCommit),
                });
            _ = EnterEndCommitWait(Round, default);
            return;
        }

        // NOTE: +1/3 prevote received, skip round
        // FIXME: Tendermint uses +2/3, should be fixed?
        if (round > Round && _heightVotes.PreVotes(round).HasOneThirdsAny)
        {
            StartRound(round);
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
        _messagePublishedSubject.OnNext(
            new ConsensusPreVoteMessage { PreVote = MakeVote(round, blockHash, VoteType.PreVote) });
    }

    private void EnterPreCommit(int round, BlockHash hash)
    {
        if (Round != round || Step >= ConsensusStep.PreCommit)
        {
            // Round and step mismatch
            return;
        }

        Step = ConsensusStep.PreCommit;
        _messagePublishedSubject.OnNext(
            new ConsensusPreCommitMessage { PreCommit = MakeVote(round, hash, VoteType.PreCommit) });
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
            _exceptionOccurredSubject.OnNext(e);
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

    private void ProcessTimeoutPreVote(int round)
    {
        if (round == Round && Step == ConsensusStep.PreVote)
        {
            EnterPreCommit(round, default);
            TimeoutProcessed?.Invoke(this, (round, ConsensusStep.PreVote));
        }
    }

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
