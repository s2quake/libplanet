using Libplanet.Net.Consensus.MessageHandlers;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusBroadcastingResponder(ISigner signer, Consensus consensus, Gossip gossip)
    : IDisposable
{
    private readonly IDisposable _subscriptions = gossip.MessageRouter.RegisterMany(
    [
        new ConsensusProposalMessageHandler(consensus),
        new ConsensusPreVoteMessageHandler(consensus),
        new ConsensusPreCommitMessageHandler(consensus),
        new ConsensusProposalClaimMessageHandler(consensus, gossip),
        new ConsensusVoteBitsMessageHandler(consensus, gossip),
        new ConsensusMaj23MessageHandler(signer, consensus, gossip),
    ]);
    private bool _disposed;

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
