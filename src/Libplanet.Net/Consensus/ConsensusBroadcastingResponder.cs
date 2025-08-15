using Libplanet.Net.Consensus.MessageHandlers;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusBroadcastingResponder : IDisposable
{
    private readonly IDisposable _subscriptions;
    private bool _disposed;

    public ConsensusBroadcastingResponder(ISigner signer, Consensus consensus, Gossip gossip)
    {
        _subscriptions = gossip.MessageRouter.RegisterMany(
        [
            new ConsensusProposalMessageHandler(consensus, PendingMessages),
            new ConsensusPreVoteMessageHandler(consensus, PendingMessages),
            new ConsensusPreCommitMessageHandler(consensus, PendingMessages),
            new ConsensusProposalClaimMessageHandler(consensus, gossip),
            new ConsensusVoteBitsMessageHandler(consensus, gossip),
            new ConsensusPreVoteMaj23MessageHandler(signer, consensus, gossip),
            new ConsensusPreCommitMaj23MessageHandler(signer, consensus, gossip),
        ]);
    }

    public MessageCollection PendingMessages { get; } = [];

    public void Dispose()
    {
        if (!_disposed)
        {
            _subscriptions.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
