using System.Reactive.Subjects;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusController : IDisposable
{
    private readonly Subject<Proposal> _proposedSubject = new();
    private readonly Subject<Vote> _preVotedSubject = new();
    private readonly Subject<Vote> _preCommittedSubject = new();
    private readonly Subject<ProposalClaim> _proposalClaimedSubject = new();
    private readonly Subject<Maj23> _majority23ObservedSubject = new();

    private readonly ISigner _signer;
    private readonly Consensus _consensus;
    private readonly Blockchain _blockchain;
    private readonly Validator _validator;
    private bool _disposed;

    internal ConsensusController(ISigner signer, Consensus consensus, Blockchain blockchain)
    {
        _signer = signer;
        _consensus = consensus;
        _blockchain = blockchain;
        _validator = consensus.Validators.GetValidator(signer.Address);
        _consensus.StepChanged.Subscribe(Consensus_StepChanged);
        _consensus.ProposalClaimed.Subscribe(Consensus_ProposalClaimed);
        _consensus.Majority23Observed.Subscribe(Consensus_Majority23Observed);
    }

    public IObservable<Vote> PreVoted => _preVotedSubject;

    public IObservable<Vote> PreCommitted => _preCommittedSubject;

    public IObservable<ProposalClaim> ProposalClaimed => _proposalClaimedSubject;

    public IObservable<Proposal> Proposed => _proposedSubject;

    public IObservable<Maj23> Majority23Observed => _majority23ObservedSubject;

    public void Dispose()
    {
        if (!_disposed)
        {
            _proposedSubject.Dispose();
            _preVotedSubject.Dispose();
            _preCommittedSubject.Dispose();
            _proposalClaimedSubject.Dispose();
            _majority23ObservedSubject.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void Consensus_StepChanged((Round Round, ConsensusStep Step, BlockHash BlockHash) e)
    {
        switch (e.Step)
        {
            case ConsensusStep.Propose:
                if (_consensus.IsProposer(_signer.Address))
                {
                    var candidateProposal = _consensus.ValidProposal;
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
                break;

            case ConsensusStep.PreVote:
                var preVote = new VoteBuilder
                {
                    Validator = _validator,
                    Height = _consensus.Height,
                    Round = e.Round.Index,
                    BlockHash = e.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Type = VoteType.PreVote,
                }.Create(_signer);
                _consensus.PreVote(preVote);
                _preVotedSubject.OnNext(preVote);
                break;

            case ConsensusStep.PreCommit:
                var preCommit = new VoteMetadata
                {
                    Height = e.Round.Height,
                    Round = e.Round.Index,
                    BlockHash = e.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = _signer.Address,
                    ValidatorPower = _consensus.Validators.GetValidator(_signer.Address).Power,
                    Type = VoteType.PreCommit,
                }.Sign(_signer);
                _consensus.PreCommit(preCommit);
                _preCommittedSubject.OnNext(preCommit);
                break;
        }
    }

    private void Consensus_Majority23Observed((Block Block, VoteType VoteType) e)
    {
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
        _consensus.AddPreVoteMaj23(maj23);
        _majority23ObservedSubject.OnNext(maj23);
    }

    private void Consensus_ProposalClaimed((Proposal Proposal, BlockHash BlockHash) e)
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
        _proposalClaimedSubject.OnNext(proposalClaim);
    }
}
