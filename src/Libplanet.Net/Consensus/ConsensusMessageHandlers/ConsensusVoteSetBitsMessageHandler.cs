using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusVoteSetBitsMessageHandler(ConsensusReactor consensusReactor, Gossip gossip)
    : MessageHandlerBase<ConsensusVoteSetBitsMessage>
{
    protected override void OnHandle(ConsensusVoteSetBitsMessage message, MessageEnvelope messageEnvelope)
    {
        var messages = consensusReactor.HandleVoteSetBits(message.VoteSetBits);
        var sender = gossip.Peers.First(peer => peer.Address.Equals(message.Validator));
        gossip.PublishMessage([sender], [.. messages]);
    }
}
