using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusBroadcaster : IDisposable
{
    private readonly IDisposable[] _disposables;
    private bool _disposed;

    public ConsensusBroadcaster(ConsensusController consensusController, Gossip gossip)
    {
        _disposables =
        [
            consensusController.PreVoted.Subscribe(
                e => gossip.Broadcast(new ConsensusPreVoteMessage { PreVote = e })),
            consensusController.PreCommitted.Subscribe(
                e => gossip.Broadcast(new ConsensusPreCommitMessage { PreCommit = e })),
            consensusController.ProposalClaimed.Subscribe(
                e => gossip.Broadcast(new ConsensusProposalClaimMessage { ProposalClaim = e })),
            consensusController.Proposed.Subscribe(
                e => gossip.Broadcast(new ConsensusProposalMessage { Proposal = e })),
            consensusController.Majority23Observed.Subscribe(
                e => gossip.Broadcast(new ConsensusMaj23Message { Maj23 = e })),
        ];
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Array.ForEach(_disposables, d => d.Dispose());
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
