using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusBroadcaster : IDisposable
{
    private readonly IDisposable[] _disposables;
    private bool _disposed;

    public ConsensusBroadcaster(ConsensusObserver consensusController, Gossip gossip)
    {
        _disposables =
        [
            consensusController.ShouldPreVote.Subscribe(
                e => gossip.Broadcast(new ConsensusPreVoteMessage { PreVote = e })),
            consensusController.ShouldPreCommit.Subscribe(
                e => gossip.Broadcast(new ConsensusPreCommitMessage { PreCommit = e })),
            consensusController.ShouldProposalClaim.Subscribe(
                e => gossip.Broadcast(new ConsensusProposalClaimMessage { ProposalClaim = e })),
            consensusController.ShouldPropose.Subscribe(
                e => gossip.Broadcast(new ConsensusProposalMessage { Proposal = e })),
            consensusController.ShouldMajority23.Subscribe(
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
