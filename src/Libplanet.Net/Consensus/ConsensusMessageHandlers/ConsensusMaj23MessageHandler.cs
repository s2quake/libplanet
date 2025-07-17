using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusMaj23MessageHandler(ConsensusReactor consensusReactor, Gossip gossip)
    : MessageHandlerBase<ConsensusMaj23Message>
{
    protected override void OnHandle(
        ConsensusMaj23Message message, MessageEnvelope messageEnvelope)
    {
        VoteSetBits? voteSetBits = consensusReactor.HandleMaj23(message.Maj23);
        if (voteSetBits is null)
        {
            return;
        }

        var sender = gossip.Peers.First(peer => peer.Address.Equals(message.Validator));
        gossip.PublishMessage(
            [sender],
            new ConsensusVoteSetBitsMessage { VoteSetBits = voteSetBits });
    }
}
