using System.Reactive.Subjects;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusObserver : IDisposable
{
    private readonly Subject<Proposal> _shouldProposeSubject = new();
    private readonly Subject<Vote> _shouldPreVoteSubject = new();
    private readonly Subject<Vote> _shouldPreCommitSubject = new();
    private readonly Subject<ProposalClaim> _shouldProposalClaimSubject = new();
    private readonly Subject<Maj23> _shouldPreVoteMaj23Subject = new();
    private readonly Subject<Maj23> _shouldPreCommitMaj23Subject = new();

    private readonly ISigner _signer;
    private readonly Consensus _consensus;
    private readonly Blockchain _blockchain;
    private bool _disposed;

    internal ConsensusObserver(ISigner signer, Consensus consensus, Blockchain blockchain)
    {
        _signer = signer;
        _consensus = consensus;
        _blockchain = blockchain;
        _consensus.StepChanged.Subscribe(Consensus_StepChanged);
        _consensus.ProposalClaimed.Subscribe(Consensus_ProposalClaimed);
        _consensus.PreVoteMaj23Observed.Subscribe(Consensus_PreVoteMaj23Observed);
        _consensus.PreCommitMaj23Observed.Subscribe(Consensus_PreCommitMaj23Observed);
    }

    public IObservable<Vote> ShouldPreVote => _shouldPreVoteSubject;

    public IObservable<Vote> ShouldPreCommit => _shouldPreCommitSubject;

    public IObservable<ProposalClaim> ShouldProposalClaim => _shouldProposalClaimSubject;

    public IObservable<Proposal> ShouldPropose => _shouldProposeSubject;

    public IObservable<Maj23> ShouldPreVoteMaj23 => _shouldPreVoteMaj23Subject;

    public IObservable<Maj23> ShouldPreCommitMaj23 => _shouldPreCommitMaj23Subject;

    public void Dispose()
    {
        if (!_disposed)
        {
            _shouldProposeSubject.Dispose();
            _shouldPreVoteSubject.Dispose();
            _shouldPreCommitSubject.Dispose();
            _shouldProposalClaimSubject.Dispose();
            _shouldPreVoteMaj23Subject.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void Consensus_StepChanged((ConsensusStep Step, BlockHash BlockHash) e)
    {
        var round = _consensus.Round;
        switch (e.Step)
        {
            case ConsensusStep.Propose:
                if (_consensus.IsProposer(_signer.Address))
                {
                    var candidateProposal = _consensus.ValidProposal;
                    var proposal = new ProposalBuilder
                    {
                        Block = candidateProposal?.Block ?? _blockchain.ProposeBlock(_signer),
                        Round = round.Index,
                        Timestamp = DateTimeOffset.UtcNow,
                        ValidRound = candidateProposal?.ValidRound ?? -1,
                    }.Create(_signer);
                    _shouldProposeSubject.OnNext(proposal);
                }
                break;

            case ConsensusStep.PreVote:
                var preVote = new VoteMetadata
                {
                    Height = _consensus.Height,
                    Round = round.Index,
                    BlockHash = e.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = _signer.Address,
                    ValidatorPower = _consensus.Validators.GetValidator(_signer.Address).Power,
                    Type = VoteType.PreVote,
                }.Sign(_signer);
                _shouldPreVoteSubject.OnNext(preVote);
                break;

            case ConsensusStep.PreCommit:
                var preCommit = new VoteMetadata
                {
                    Height = round.Height,
                    Round = round.Index,
                    BlockHash = e.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = _signer.Address,
                    ValidatorPower = _consensus.Validators.GetValidator(_signer.Address).Power,
                    Type = VoteType.PreCommit,
                }.Sign(_signer);
                _shouldPreCommitSubject.OnNext(preCommit);
                break;
        }
    }

    private void Consensus_PreVoteMaj23Observed(Block Block)
    {
        var round = _consensus.Round;
        var maj23 = new Maj23Metadata
        {
            Height = _consensus.Height,
            Round = round.Index,
            BlockHash = Block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = _signer.Address,
            VoteType = VoteType.PreVote,
        }.Sign(_signer);
        _shouldPreVoteMaj23Subject.OnNext(maj23);
    }

    private void Consensus_PreCommitMaj23Observed(Block Block)
    {
        var round = _consensus.Round;
        var maj23 = new Maj23Metadata
        {
            Height = _consensus.Height,
            Round = round.Index,
            BlockHash = Block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = _signer.Address,
            VoteType = VoteType.PreCommit,
        }.Sign(_signer);
        _shouldPreCommitMaj23Subject.OnNext(maj23);
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
        _shouldProposalClaimSubject.OnNext(proposalClaim);
    }
}
