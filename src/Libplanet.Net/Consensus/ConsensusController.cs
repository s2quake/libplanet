using System.Reactive;
using System.Reactive.Subjects;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Extensions;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusController
{
    private readonly Subject<Vote> _preVotedSubject = new();
    private readonly Subject<Vote> _preCommittedSubject = new();
    private readonly Subject<Maj23> _shouldQuorumReachSubject = new();
    private readonly Subject<ProposalClaim> _shouldProposalClaimSubject = new();
    private readonly Subject<Proposal> _proposedSubject = new();

    private readonly ISigner _signer;
    private readonly Consensus _consensus;
    private readonly Blockchain _blockchain;

    internal ConsensusController(ISigner signer, Consensus consensus, Blockchain blockchain)
    {
        _signer = signer;
        _consensus = consensus;
        _blockchain = blockchain;
        _consensus.StepChanged.Subscribe(Consensus_StepChanged);
        _consensus.PreVoted.Subscribe(Consensus_PreVoted);
        _consensus.ProposalRejected.Subscribe(Consensus_ProposalRejected);
        _consensus.QuorumReached.Subscribe(Consensus_BlockDecided);
    }

    private void Consensus_BlockDecided((Block Block, VoteType VoteType) e)
    {
        // throw new NotImplementedException();
        var round = _consensus.Round;

        var maj23 = new Maj23Metadata
        {
            Height = _consensus.Height,
            Round = round.Index,
            BlockHash = e.Block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = _signer.Address,
            VoteType = e.VoteType,
        }.Sign(_signer);
    }

    private void Consensus_ProposalRejected((Proposal Proposal, BlockHash BlockHash) e)
    {
        var round = _consensus.Round;

        var proposalClaim = new ProposalClaimMetadata
        {
            Height = _consensus.Height,
            Round = round.Index,
            BlockHash = e.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = _signer.Address,
        }.Sign(_signer);
    }

    private void Consensus_PreVoted(Vote vote)
    {
        var round = _consensus.Round;
        var proposal = _consensus.Proposal;
        // if (round.PreVotes.BlockHash != default)
        // if (proposal is not null && !proposal.BlockHash.Equals(hash3))
        //         {
        //             // +2/3 votes were collected and is not equal to proposal's,
        //             // remove invalid proposal.
        //             Proposal = null;

        //             var proposalClaim = new ProposalClaimMetadata
        //             {
        //                 Height = Height,
        //                 Round = roundIndex,
        //                 BlockHash = hash3,
        //                 Timestamp = DateTimeOffset.UtcNow,
        //                 Validator = signer.Address,
        //             }.Sign(signer);
        //             _shouldProposalClaimSubject.OnNext(proposalClaim);
        //         }
    }

    public IObservable<Vote> PreVoted => _preVotedSubject;

    public IObservable<Vote> PreCommitted => _preCommittedSubject;

    // public IObservable<Maj23> ShouldQuorumReach => _shouldQuorumReachSubject;

    // public IObservable<ProposalClaim> ShouldProposalClaim => _shouldProposalClaimSubject;

    public IObservable<Proposal> Proposed => _proposedSubject;

    private void Consensus_StepChanged((Round Round, ConsensusStep Step, BlockHash BlockHash) e)
    {
        switch (e.Step)
        {
            case ConsensusStep.Propose:
                {
                    if (_consensus.IsProposer(_signer.Address))
                    {
                        var candidateProposal = _consensus.CandidatedProposal;
                        var proposal = new ProposalBuilder
                        {
                            Block = candidateProposal?.Block ?? _blockchain.ProposeBlock(_signer),
                            Round = e.Round.Index,
                            Timestamp = DateTimeOffset.UtcNow,
                            ValidRound = candidateProposal?.ValidRound ?? -1,
                        }.Create(_signer);
                        _consensus.Propose(proposal);
                        _proposedSubject.OnNext(proposal);
                    }
                }
                break;
            case ConsensusStep.PreVote:
                {
                    var vote = new VoteMetadata
                    {
                        Height = _consensus.Height,
                        Round = e.Round.Index,
                        BlockHash = e.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = _signer.Address,
                        ValidatorPower = _consensus.Validators.GetValidator(_signer.Address).Power,
                        Type = VoteType.PreVote,
                    }.Sign(_signer);
                    _consensus.PreVote(vote);
                    _preVotedSubject.OnNext(vote);
                }
                break;
            case ConsensusStep.PreCommit:
                {
                    var vote = new VoteMetadata
                    {
                        Height = e.Round.Height,
                        Round = e.Round.Index,
                        BlockHash = e.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = _signer.Address,
                        ValidatorPower = _consensus.Validators.GetValidator(_signer.Address).Power,
                        Type = VoteType.PreCommit,
                    }.Sign(_signer);
                    _consensus.PreCommit(vote);
                    _preCommittedSubject.OnNext(vote);
                }
                break;
            default:
                // No action needed for other steps.
                break;
        }
    }
}
