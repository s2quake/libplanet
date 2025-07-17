using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusVoteSetBitsMessageHandler(ConsensusReactor consensusReactor, Gossip gossip)
    : MessageHandlerBase<ConsensusVoteSetBitsMessage>
{
    protected override async ValueTask OnHandleAsync(
        ConsensusVoteSetBitsMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        var messages = consensusReactor.HandleVoteSetBits(message.VoteSetBits);
        var sender = gossip.Peers.First(peer => peer.Address.Equals(message.Validator));
        gossip.PublishMessage([sender], [.. messages]);
        await ValueTask.CompletedTask;
    }
}
