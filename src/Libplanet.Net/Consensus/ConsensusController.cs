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

    private readonly Subject<Vote> _shouldPreVoteSubject = new();
    private readonly Subject<Vote> _shouldPreCommitSubject = new();
    private readonly Subject<Maj23> _shouldQuorumReachSubject = new();
    private readonly Subject<ProposalClaim> _shouldProposalClaimSubject = new();
    private readonly Subject<Proposal> _shouldProposeSubject = new();


    private readonly ISigner _signer;
    private readonly Consensus _consensus;
    private readonly Blockchain _blockchain;

    public ConsensusController(ISigner signer, Consensus consensus, Blockchain blockchain)
    {
        _signer = signer;
        _consensus = consensus;
        _blockchain = blockchain;
        _consensus.StepChanged.Subscribe(Consensus_StepChanged);
    }

    private void Consensus_StepChanged(ConsensusStep step)
    {
        switch (step)
        {
            case ConsensusStep.Propose:
                {
                    if (_consensus.IsSigner(_signer.Address))
                    {
                        var proposal = new ProposalBuilder
                        {
                            Block = _blockchain.ProposeBlock(_signer),
                            Round = _consensus.Round.Index,
                            Timestamp = DateTimeOffset.UtcNow,
                            ValidRound = -1,
                        }.Create(_signer);
                        _consensus.Propose(proposal);
                        _shouldProposeSubject.OnNext(proposal);
                    }
                }
                break;
            case ConsensusStep.PreVote:
                {
                    // var vote = new VoteMetadata
                    // {
                    //     Height = _consensus.Height,
                    //     Round = _consensus.Round,
                    //     BlockHash = blockHash,
                    //     Timestamp = DateTimeOffset.UtcNow,
                    //     Validator = signer.Address,
                    //     ValidatorPower = Validators.GetValidator(signer.Address).Power,
                    //     Type = VoteType.PreVote,
                    // }.Sign(signer);
                    // Step = ConsensusStep.PreVote;
                }
                break;
            case ConsensusStep.PreCommit:
                break;
            default:
                // No action needed for other steps.
                break;
        }
    }
}
