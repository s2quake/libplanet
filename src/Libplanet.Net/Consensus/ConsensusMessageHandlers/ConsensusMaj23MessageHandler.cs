using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusMaj23MessageHandler(ConsensusService consensusService, Gossip gossip)
    : MessageHandlerBase<ConsensusMaj23Message>
{
    protected override async ValueTask OnHandleAsync(
        ConsensusMaj23Message message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        VoteSetBits? voteSetBits = consensusService.HandleMaj23(message.Maj23);
        if (voteSetBits is null)
        {
            return;
        }

        var sender = gossip.Peers.First(peer => peer.Address.Equals(message.Validator));
        gossip.PublishMessage(
            [sender],
            new ConsensusVoteSetBitsMessage { VoteSetBits = voteSetBits });
        await ValueTask.CompletedTask;
    }
}
