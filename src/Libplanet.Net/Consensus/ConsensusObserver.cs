using System.Reactive.Subjects;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusObserver : IDisposable
{
    private readonly Subject<Proposal> _shouldProposeSubject = new();
    private readonly Subject<Vote> _shouldPreVoteSubject = new();
    private readonly Subject<Vote> _shouldPreCommitSubject = new();
    private readonly Subject<ProposalClaim> _shouldProposalClaimSubject = new();
    private readonly Subject<Maj23> _shouldMajority23Subject = new();

    private readonly ISigner _signer;
    private readonly Consensus _consensus;
    private readonly Blockchain _blockchain;
    private readonly Validator _validator;
    private bool _disposed;

    internal ConsensusObserver(ISigner signer, Consensus consensus, Blockchain blockchain)
    {
        _signer = signer;
        _consensus = consensus;
        _blockchain = blockchain;
        _validator = consensus.Validators.GetValidator(signer.Address);
        _consensus.StepChanged.Subscribe(Consensus_StepChanged);
        _consensus.ProposalClaimed.Subscribe(Consensus_ProposalClaimed);
        _consensus.Majority23Observed.Subscribe(Consensus_Majority23Observed);
    }

    public IObservable<Vote> ShouldPreVote => _shouldPreVoteSubject;

    public IObservable<Vote> ShouldPreCommit => _shouldPreCommitSubject;

    public IObservable<ProposalClaim> ShouldProposalClaim => _shouldProposalClaimSubject;

    public IObservable<Proposal> ShouldPropose => _shouldProposeSubject;

    public IObservable<Maj23> ShouldMajority23 => _shouldMajority23Subject;

    public void Dispose()
    {
        if (!_disposed)
        {
            _shouldProposeSubject.Dispose();
            _shouldPreVoteSubject.Dispose();
            _shouldPreCommitSubject.Dispose();
            _shouldProposalClaimSubject.Dispose();
            _shouldMajority23Subject.Dispose();
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
                var preVote = new VoteBuilder
                {
                    Validator = _validator,
                    Height = _consensus.Height,
                    Round = round.Index,
                    BlockHash = e.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Type = VoteType.PreVote,
                }.Create(_signer);
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
        _shouldMajority23Subject.OnNext(maj23);
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
